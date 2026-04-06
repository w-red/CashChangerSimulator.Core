using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Device.Virtual;
using Microsoft.PointOfService;
using R3;

namespace CashChangerSimulator.Device.PosForDotNet.Coordination;

/// <summary>釣銭機の各コンポーネントからの通知を集約する調整クラス。</summary>
/// <remarks>内部状態の変化を UPOS イベントへ変換して通知します。</remarks>
public class StatusCoordinator(
    ICashChangerStatusSink sink,
    OverallStatusAggregator statusAggregator,
    HardwareStatusManager hardwareStatusManager,
    DepositController depositController,
    DispenseController dispenseController) : IDisposable
{
    private readonly CompositeDisposable disposables = [];
    private bool disposed;
    private bool isStarted;
    private bool wasFixed;

    /// <summary>現在のデバイスステータスを取得します。</summary>
    public CashChangerStatus LastCashChangerStatus { get; private set; } = CashChangerStatus.OK;

    /// <summary>現在のフルステータスを取得します。</summary>
    public CashChangerFullStatus LastFullStatus { get; private set; } = CashChangerFullStatus.OK;

    /// <inheritdoc/>
    public void Start()
    {
        if (disposed || isStarted)
        {
            return;
        }

        isStarted = true;

        // Initialize with current values
        LastFullStatus = statusAggregator.FullStatus.CurrentValue switch
        {
            CashStatus.Full => CashChangerFullStatus.Full,
            CashStatus.NearFull => CashChangerFullStatus.NearFull,
            _ => CashChangerFullStatus.OK
        };
        LastCashChangerStatus = statusAggregator.DeviceStatus.CurrentValue switch
        {
            CashStatus.Empty => CashChangerStatus.Empty,
            CashStatus.NearEmpty => CashChangerStatus.NearEmpty,
            _ => CashChangerStatus.OK
        };

        disposables.Add(statusAggregator.DeviceStatus.Subscribe(status =>
        {
            if (disposed)
            {
                return;
            }

            var newDeviceStatus = status switch
            {
                CashStatus.Empty => CashChangerStatus.Empty,
                CashStatus.NearEmpty => CashChangerStatus.NearEmpty,
                CashStatus.Normal => CashChangerStatus.OK, // Explicitly map Normal to OK
                _ => CashChangerStatus.OK
            };

            if (newDeviceStatus != LastCashChangerStatus)
            {
                var previousStatus = LastCashChangerStatus;
                LastCashChangerStatus = newDeviceStatus;

                if (newDeviceStatus == CashChangerStatus.OK &&
                    previousStatus is CashChangerStatus.Empty or CashChangerStatus.NearEmpty)
                {
                    sink.FireEvent(new StatusUpdateEventArgs((int)UposCashChangerStatusUpdateCode.EmptyOk));
                }
                else
                {
                    var code = newDeviceStatus switch
                    {
                        CashChangerStatus.Empty => (int)UposCashChangerStatusUpdateCode.Empty,
                        CashChangerStatus.NearEmpty => (int)UposCashChangerStatusUpdateCode.NearEmpty,
                        _ => (int)UposCashChangerStatusUpdateCode.Ok
                    };
                    sink.FireEvent(new StatusUpdateEventArgs(code));
                }
            }
        }));

        disposables.Add(statusAggregator.FullStatus.Subscribe(status =>
        {
            if (disposed)
            {
                return;
            }

            var newFullStatus = status switch
            {
                CashStatus.Full => CashChangerFullStatus.Full,
                CashStatus.NearFull => CashChangerFullStatus.NearFull,
                _ => CashChangerFullStatus.OK
            };

            if (newFullStatus != LastFullStatus)
            {
                var previousFullStatus = LastFullStatus;
                LastFullStatus = newFullStatus;

                if (newFullStatus == CashChangerFullStatus.OK &&
                    previousFullStatus is CashChangerFullStatus.Full or CashChangerFullStatus.NearFull)
                {
                    sink.FireEvent(new StatusUpdateEventArgs((int)UposCashChangerStatusUpdateCode.FullOk));
                }
                else
                {
                    sink.FireEvent(new StatusUpdateEventArgs((int)newFullStatus));
                }
            }
        }));

        disposables.Add(hardwareStatusManager.IsConnected.Subscribe(connected =>
        {
            if (disposed)
            {
                return;
            }

            var code = connected ? UposCashChangerStatusUpdateCode.Inserted : UposCashChangerStatusUpdateCode.Removed;
            sink.FireEvent(new StatusUpdateEventArgs((int)code));
        }));

        disposables.Add(hardwareStatusManager.IsCollectionBoxRemoved.Skip(1).Subscribe(removed =>
        {
            if (disposed)
            {
                return;
            }

            var code = removed ? UposCashChangerStatusUpdateCode.Removed : UposCashChangerStatusUpdateCode.Inserted;
            sink.FireEvent(new StatusUpdateEventArgs((int)code));
        }));

        disposables.Add(hardwareStatusManager.IsJammed.Subscribe(jammed =>
        {
            if (disposed)
            {
                return;
            }

            if (jammed)
            {
                sink.FireEvent(new StatusUpdateEventArgs((int)UposCashChangerStatusUpdateCode.Jam));
            }
            else
            {
                // Re-evaluate from aggregator when jam is cleared
                var currentAggregatedStatus = statusAggregator.DeviceStatus.CurrentValue;
                LastCashChangerStatus = currentAggregatedStatus switch
                {
                    CashStatus.Empty => CashChangerStatus.Empty,
                    CashStatus.NearEmpty => CashChangerStatus.NearEmpty,
                    _ => CashChangerStatus.OK
                };
                sink.FireEvent(new StatusUpdateEventArgs((int)UposCashChangerStatusUpdateCode.Ok));

                // If it was still Empty/NearEmpty, fire those too?
                // Actually, UPOS Ok event often implies clearing of error state.
            }
        }));

        // Handle deposit state changes and event firing
        disposables.Add(depositController.Changed
            .Select(_ => (depositController.IsFixed, depositController.DepositStatus, depositController.IsPaused, depositController.DepositAmount))
            .DistinctUntilChanged() // [FIX] Prevent multiple events for the same state transition
            .Subscribe(state =>
        {
            if (disposed)
            {
                return;
            }

            bool isFixed = state.IsFixed;
            bool isPaused = state.IsPaused;
            var currentStatus = state.DepositStatus;

            // [LIFECYCLE] Reset wasFixed flag at the start of a deposit session.
            if (currentStatus == DeviceDepositStatus.Start)
            {
                wasFixed = false;
                return;
            }

            // [UPOS] Check if we should fire DataEvent
            bool eligibleForDataEvent = (currentStatus == DeviceDepositStatus.Counting || currentStatus == DeviceDepositStatus.Validation)
                && !isPaused && sink.DataEventEnabled;

            if (eligibleForDataEvent)
            {
                if (sink.RealTimeDataEnabled)
                {
                    // [REAL-TIME] Fire on every change (tracked money)
                    sink.FireEvent(new DataEventArgs(0));
                }
                else if (isFixed && !wasFixed)
                {
                    // [BUFFERED] Fire exactly once when FixDeposit is called.
                    wasFixed = true;
                    sink.FireEvent(new DataEventArgs(0));
                }
            }

            // [LIFECYCLE] Ensure _wasFixed is reset if we leave the active state (e.g. End)
            if (currentStatus == DeviceDepositStatus.End || currentStatus == DeviceDepositStatus.None)
            {
                wasFixed = false;
            }
        }));

        disposables.Add(dispenseController.Changed
            .Select(_ => dispenseController.IsBusy)
            .DistinctUntilChanged()
            .Pairwise()
            .Subscribe(x =>
            {
                bool wasBusy = x.Previous;
                bool isBusy = x.Current;

                sink.SetAsyncProcessing(isBusy);

                // Transition to Idle (isBusy: true -> false)
                if (wasBusy && !isBusy)
                {
                    // Check if it was naturally completed or cancelled
                    if (dispenseController.LastErrorCode != DeviceErrorCode.Cancelled)
                    {
                        // Update async result properties in the sink
                        sink.AsyncResultCode = (int)dispenseController.LastErrorCode;
                        sink.AsyncResultCodeExtended = dispenseController.LastErrorCodeExtended;

                        // [COMPAT] Fire AsyncFinished (91) for tests and UI compatibility
                        sink.FireEvent(new StatusUpdateEventArgs((int)UposCashChangerStatusUpdateCode.AsyncFinished));

                        // Result-specific event
                        if (dispenseController.LastErrorCode == DeviceErrorCode.Success)
                        {
                            sink.FireEvent(new OutputCompleteEventArgs(0));
                        }
                    }
                }
            }));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        disposables.Dispose();
        GC.SuppressFinalize(this);
    }
}
