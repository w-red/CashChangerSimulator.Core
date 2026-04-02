using CashChangerSimulator.Device.Virtual;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Opos;
using Microsoft.PointOfService;
using CashChangerSimulator.Device;
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
    private readonly CompositeDisposable _disposables = [];
    private CashChangerStatus _lastCashChangerStatus = CashChangerStatus.OK;
    private CashChangerFullStatus _lastFullStatus = CashChangerFullStatus.OK;
    private bool _disposed;
    private bool _isStarted;
    private bool _wasFixed;

    /// <summary>現在のデバイスステータス。</summary>
    public CashChangerStatus LastCashChangerStatus => _lastCashChangerStatus;

    /// <summary>現在のフルステータス。</summary>
    public CashChangerFullStatus LastFullStatus => _lastFullStatus;

    public void Start()
    {
        if (_disposed || _isStarted) return;
        _isStarted = true;

        // Initialize with current values
        _lastFullStatus = statusAggregator.FullStatus.CurrentValue switch
        {
            CashStatus.Full => CashChangerFullStatus.Full,
            CashStatus.NearFull => CashChangerFullStatus.NearFull,
            _ => CashChangerFullStatus.OK
        };
        _lastCashChangerStatus = statusAggregator.DeviceStatus.CurrentValue switch
        {
            CashStatus.Empty => CashChangerStatus.Empty,
            CashStatus.NearEmpty => CashChangerStatus.NearEmpty,
            _ => CashChangerStatus.OK
        };

        _disposables.Add(statusAggregator.DeviceStatus.Subscribe(status =>
        {
            if (_disposed) return;
            var newDeviceStatus = status switch
            {
                CashStatus.Empty => CashChangerStatus.Empty,
                CashStatus.NearEmpty => CashChangerStatus.NearEmpty,
                CashStatus.Normal => CashChangerStatus.OK, // Explicitly map Normal to OK
                _ => CashChangerStatus.OK
            };

            if (newDeviceStatus != _lastCashChangerStatus)
            {
                var previousStatus = _lastCashChangerStatus;
                _lastCashChangerStatus = newDeviceStatus;

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

        _disposables.Add(statusAggregator.FullStatus.Subscribe(status =>
        {
            if (_disposed) return;
            var newFullStatus = status switch
            {
                CashStatus.Full => CashChangerFullStatus.Full,
                CashStatus.NearFull => CashChangerFullStatus.NearFull,
                _ => CashChangerFullStatus.OK
            };

            if (newFullStatus != _lastFullStatus)
            {
                var previousFullStatus = _lastFullStatus;
                _lastFullStatus = newFullStatus;

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

        _disposables.Add(hardwareStatusManager.IsConnected.Subscribe(connected =>
        {
            if (_disposed) return;
            var code = connected ? UposCashChangerStatusUpdateCode.Inserted : UposCashChangerStatusUpdateCode.Removed;
            sink.FireEvent(new StatusUpdateEventArgs((int)code));
        }));

        _disposables.Add(hardwareStatusManager.IsCollectionBoxRemoved.Skip(1).Subscribe(removed =>
        {
            if (_disposed) return;
            var code = removed ? UposCashChangerStatusUpdateCode.Removed : UposCashChangerStatusUpdateCode.Inserted;
            sink.FireEvent(new StatusUpdateEventArgs((int)code));
        }));

        _disposables.Add(hardwareStatusManager.IsJammed.Subscribe(jammed =>
        {
            if (_disposed) return;
            if (jammed)
            {
                sink.FireEvent(new StatusUpdateEventArgs((int)UposCashChangerStatusUpdateCode.Jam));
            }
            else
            {
                // Re-evaluate from aggregator when jam is cleared
                var currentAggregatedStatus = statusAggregator.DeviceStatus.CurrentValue;
                _lastCashChangerStatus = currentAggregatedStatus switch
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
        _disposables.Add(depositController.Changed
            .Select(_ => (depositController.IsFixed, depositController.DepositStatus, depositController.IsPaused, depositController.DepositAmount))
            .DistinctUntilChanged() // [FIX] Prevent multiple events for the same state transition
            .Subscribe(state =>
        {
            if (_disposed) return;
            
            bool isFixed = state.IsFixed;
            bool isPaused = state.IsPaused;
            var currentStatus = state.DepositStatus;

            // [LIFECYCLE] Reset wasFixed flag at the start of a deposit session.
            if (currentStatus == DeviceDepositStatus.Start)
            {
                _wasFixed = false;
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
                else if (isFixed && !_wasFixed)
                {
                    // [BUFFERED] Fire exactly once when FixDeposit is called.
                    _wasFixed = true; 
                    sink.FireEvent(new DataEventArgs(0));
                }
            }
            
            // [LIFECYCLE] Ensure _wasFixed is reset if we leave the active state (e.g. End)
            if (currentStatus == DeviceDepositStatus.End || currentStatus == DeviceDepositStatus.None)
            {
                _wasFixed = false;
            }
        }));

        _disposables.Add(dispenseController.Changed.Subscribe(_ =>
        {
            if (_disposed) return;
            sink.SetAsyncProcessing(dispenseController.IsBusy);
        }));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _disposables.Dispose();
        GC.SuppressFinalize(this);
    }
}
