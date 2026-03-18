using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Opos;
using Microsoft.PointOfService;
using R3;

namespace CashChangerSimulator.Device.Coordination;

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

    /// <summary>現在のデバイスステータス。</summary>
    public CashChangerStatus LastCashChangerStatus => _lastCashChangerStatus;

    /// <summary>現在のフルステータス。</summary>
    public CashChangerFullStatus LastFullStatus => _lastFullStatus;

    /// <summary>全ステータスの購読を開始します。</summary>
    public void Start()
    {
        if (_disposed) return;
        _disposables.Add(statusAggregator.DeviceStatus.Subscribe(status =>
        {
            if (_disposed) return;
            var newDeviceStatus = status switch
            {
                CashStatus.Empty => CashChangerStatus.Empty,
                CashStatus.NearEmpty => CashChangerStatus.NearEmpty,
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

        _disposables.Add(depositController.Changed.Subscribe(_ =>
        {
            if (_disposed) return;
            if (depositController.DepositStatus == CashDepositStatus.Count && !depositController.IsPaused && sink.DataEventEnabled)
            {
                if (sink.RealTimeDataEnabled || depositController.IsFixed)
                {
                    sink.FireEvent(new DataEventArgs(0));
                }
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
