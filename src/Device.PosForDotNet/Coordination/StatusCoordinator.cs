using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Device.Virtual;
using Microsoft.PointOfService;
using R3;

namespace CashChangerSimulator.Device.PosForDotNet.Coordination;

/// <summary>釣銭機の各コンポーネントからの通知を集約する調整クラス。</summary>
/// <remarks>内部状態の変化を UPOS イベントへ変換して通知します。</remarks>
public sealed class StatusCoordinator(
    ICashChangerStatusSink sink,
    OverallStatusAggregator statusAggregator,
    HardwareStatusManager hardwareStatusManager,
    DepositController depositController,
    DispenseController dispenseController) : IDisposable
{
    private readonly CompositeDisposable disposables = [];
    private int _disposed;
    private int _isStarted;
    private int _wasFixed;

    /// <summary>現在のデバイスステータスを取得します。</summary>
    public Core.Models.CashChangerStatus LastCashChangerStatus { get; private set; } = CashChangerSimulator.Core.Models.CashChangerStatus.OK;

    /// <summary>現在のフルステータスを取得します。</summary>
    public Core.Models.CashChangerFullStatus LastFullStatus { get; private set; } = CashChangerSimulator.Core.Models.CashChangerFullStatus.OK;

    /// <inheritdoc/>
    public void Start()
    {
        if (Volatile.Read(ref _disposed) == 1 || Volatile.Read(ref _isStarted) == 1)
        {
            return;
        }

        Interlocked.Exchange(ref _isStarted, 1);

        InitializeStatus();

        ObserveAggregatorStatus();
        ObserveHardwareEvents();
        ObserveDepositEvents();
        ObserveDispenseEvents();
    }

    private void InitializeStatus()
    {
        LastFullStatus = statusAggregator.FullStatus.CurrentValue switch
        {
            CashStatus.Full => CashChangerSimulator.Core.Models.CashChangerFullStatus.Full,
            CashStatus.NearFull => CashChangerSimulator.Core.Models.CashChangerFullStatus.NearFull,
            _ => CashChangerSimulator.Core.Models.CashChangerFullStatus.OK
        };

        LastCashChangerStatus = statusAggregator.DeviceStatus.CurrentValue switch
        {
            CashStatus.Empty => CashChangerSimulator.Core.Models.CashChangerStatus.Empty,
            CashStatus.NearEmpty => CashChangerSimulator.Core.Models.CashChangerStatus.NearEmpty,
            _ => CashChangerSimulator.Core.Models.CashChangerStatus.OK
        };
    }

    private void ObserveAggregatorStatus()
    {
        disposables.Add(statusAggregator.DeviceStatus.Subscribe(OnDeviceStatusChanged));
        disposables.Add(statusAggregator.FullStatus.Subscribe(OnFullStatusChanged));
    }

    private void OnDeviceStatusChanged(CashStatus status)
    {
        if (Volatile.Read(ref _disposed) == 1) return;

        var newDeviceStatus = status switch
        {
            CashStatus.Empty => CashChangerSimulator.Core.Models.CashChangerStatus.Empty,
            CashStatus.NearEmpty => CashChangerSimulator.Core.Models.CashChangerStatus.NearEmpty,
            _ => CashChangerSimulator.Core.Models.CashChangerStatus.OK
        };

        if (newDeviceStatus != LastCashChangerStatus)
        {
            var previousStatus = LastCashChangerStatus;
            LastCashChangerStatus = newDeviceStatus;
            FireDeviceStatusEvent(newDeviceStatus, previousStatus);
        }
    }

    private void FireDeviceStatusEvent(Core.Models.CashChangerStatus newStatus, Core.Models.CashChangerStatus previousStatus)
    {
        if (newStatus == CashChangerSimulator.Core.Models.CashChangerStatus.OK &&
            previousStatus is CashChangerSimulator.Core.Models.CashChangerStatus.Empty or CashChangerSimulator.Core.Models.CashChangerStatus.NearEmpty)
        {
            sink.FireEvent(new StatusUpdateEventArgs((int)UposCashChangerStatusUpdateCode.EmptyOk));
        }
        else
        {
            var code = newStatus switch
            {
                CashChangerSimulator.Core.Models.CashChangerStatus.Empty => (int)UposCashChangerStatusUpdateCode.Empty,
                CashChangerSimulator.Core.Models.CashChangerStatus.NearEmpty => (int)UposCashChangerStatusUpdateCode.NearEmpty,
                _ => (int)UposCashChangerStatusUpdateCode.Ok
            };
            sink.FireEvent(new StatusUpdateEventArgs(code));
        }
    }

    private void OnFullStatusChanged(CashStatus status)
    {
        if (Volatile.Read(ref _disposed) == 1) return;

        var newFullStatus = status switch
        {
            CashStatus.Full => CashChangerSimulator.Core.Models.CashChangerFullStatus.Full,
            CashStatus.NearFull => CashChangerSimulator.Core.Models.CashChangerFullStatus.NearFull,
            _ => CashChangerSimulator.Core.Models.CashChangerFullStatus.OK
        };

        if (newFullStatus != LastFullStatus)
        {
            var previousFullStatus = LastFullStatus;
            LastFullStatus = newFullStatus;
            FireFullStatusEvent(newFullStatus, previousFullStatus);
        }
    }

    private void FireFullStatusEvent(Core.Models.CashChangerFullStatus newStatus, Core.Models.CashChangerFullStatus previousStatus)
    {
        if (newStatus == CashChangerSimulator.Core.Models.CashChangerFullStatus.OK &&
            previousStatus is CashChangerSimulator.Core.Models.CashChangerFullStatus.Full or CashChangerSimulator.Core.Models.CashChangerFullStatus.NearFull)
        {
            sink.FireEvent(new StatusUpdateEventArgs((int)UposCashChangerStatusUpdateCode.FullOk));
        }
        else
        {
            sink.FireEvent(new StatusUpdateEventArgs((int)newStatus));
        }
    }

    private void ObserveHardwareEvents()
    {
        disposables.Add(hardwareStatusManager.IsConnected.Skip(1).Subscribe(connected =>
        {
            if (Volatile.Read(ref _disposed) == 1) return;
            var code = connected ? UposCashChangerStatusUpdateCode.Inserted : UposCashChangerStatusUpdateCode.Removed;
            sink.FireEvent(new StatusUpdateEventArgs((int)code));
        }));

        disposables.Add(hardwareStatusManager.IsCollectionBoxRemoved.Skip(1).Subscribe(removed =>
        {
            if (Volatile.Read(ref _disposed) == 1) return;
            var code = removed ? UposCashChangerStatusUpdateCode.Removed : UposCashChangerStatusUpdateCode.Inserted;
            sink.FireEvent(new StatusUpdateEventArgs((int)code));
        }));

        disposables.Add(hardwareStatusManager.IsJammed.Skip(1).Subscribe(OnJammedChanged));
    }

    private void OnJammedChanged(bool jammed)
    {
        if (Volatile.Read(ref _disposed) == 1) return;

        if (jammed)
        {
            sink.FireEvent(new StatusUpdateEventArgs((int)UposCashChangerStatusUpdateCode.Jam));
        }
        else
        {
            LastCashChangerStatus = statusAggregator.DeviceStatus.CurrentValue switch
            {
                CashStatus.Empty => CashChangerSimulator.Core.Models.CashChangerStatus.Empty,
                CashStatus.NearEmpty => CashChangerSimulator.Core.Models.CashChangerStatus.NearEmpty,
                _ => CashChangerSimulator.Core.Models.CashChangerStatus.OK
            };
            sink.FireEvent(new StatusUpdateEventArgs((int)UposCashChangerStatusUpdateCode.Ok));
        }
    }

    private void ObserveDepositEvents()
    {
        disposables.Add(depositController.DataEvents.Subscribe(e =>
        {
            if (Volatile.Read(ref _disposed) == 1 || !sink.RealTimeDataEnabled) return;
            sink.FireEvent(new DataEventArgs(e.Status));
        }));

        disposables.Add(depositController.Changed
            .Select(_ => (depositController.IsFixed, depositController.DepositStatus, depositController.IsPaused))
            .DistinctUntilChanged()
            .Subscribe(OnDepositStateChanged));
    }

    private void OnDepositStateChanged((bool IsFixed, DeviceDepositStatus DepositStatus, bool IsPaused) state)
    {
        if (Volatile.Read(ref _disposed) == 1) return;

        if (state.DepositStatus == DeviceDepositStatus.Start)
        {
            Interlocked.Exchange(ref _wasFixed, 0);
            return;
        }

        bool eligibleForDataEvent = (state.DepositStatus == DeviceDepositStatus.Counting || state.DepositStatus == DeviceDepositStatus.Validation)
            && !state.IsPaused && sink.DataEventEnabled;

        if (eligibleForDataEvent && !sink.RealTimeDataEnabled && state.IsFixed && Volatile.Read(ref _wasFixed) == 0)
        {
            Interlocked.Exchange(ref _wasFixed, 1);
            sink.FireEvent(new DataEventArgs(0));
        }

        if (state.DepositStatus == DeviceDepositStatus.End || state.DepositStatus == DeviceDepositStatus.None)
        {
            Interlocked.Exchange(ref _wasFixed, 0);
        }
    }

    private void ObserveDispenseEvents()
    {
        disposables.Add(dispenseController.Changed
            .Select(_ => dispenseController.IsBusy)
            .Prepend(dispenseController.IsBusy)
            .DistinctUntilChanged()
            .Pairwise()
            .Subscribe(OnDispenseBusyChanged));
    }

    private void OnDispenseBusyChanged((bool Previous, bool Current) x)
    {
        if (Volatile.Read(ref _disposed) == 1) return;

        sink.SetAsyncProcessing(x.Current);

        if (x.Previous && !x.Current && dispenseController.LastErrorCode != DeviceErrorCode.Cancelled)
        {
            sink.AsyncResultCode = (int)dispenseController.LastErrorCode;
            sink.AsyncResultCodeExtended = dispenseController.LastErrorCodeExtended;
            sink.FireEvent(new StatusUpdateEventArgs((int)UposCashChangerStatusUpdateCode.AsyncFinished));

            if (dispenseController.LastErrorCode == DeviceErrorCode.Success)
            {
                sink.FireEvent(new OutputCompleteEventArgs(0));
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        disposables.Dispose();
        GC.SuppressFinalize(this);
    }
}
