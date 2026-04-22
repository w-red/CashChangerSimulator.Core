using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Core.Transactions;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.PosForDotNet;

/// <summary>デバイスから発火されるイベントを購読し、取引履歴を記録するオブザーバークラス。</summary>
public sealed class DeviceEventHistoryObserver : IDisposable
{
    private readonly InternalSimulatorCashChanger device;
    private readonly TransactionHistory history;

    /// <summary>Initializes a new instance of the <see cref="DeviceEventHistoryObserver"/> class.オブザーバーを初期化し、イベントを購読します。</summary>
    public DeviceEventHistoryObserver(InternalSimulatorCashChanger device, TransactionHistory history)
    {
        this.device = device ?? throw new ArgumentNullException(nameof(device));
        this.history = history ?? throw new ArgumentNullException(nameof(history));

        this.device.OnEventQueued += HandleDeviceEvent;
    }

    private void HandleDeviceEvent(EventArgs e)
    {
        if (e is DataEventArgs)
        {
            history.Add(new TransactionEntry(
                DateTimeOffset.Now,
                TransactionType.DataEvent,
                0,
                new Dictionary<DenominationKey, int>()));
        }
        else if (e is StatusUpdateEventArgs se)
        {
            if (se.Status == (int)UposCashChangerStatusUpdateCode.Jam)
            {
                history.Add(new TransactionEntry(
                    DateTimeOffset.Now,
                    TransactionType.HardwareError,
                    0,
                    new Dictionary<DenominationKey, int>()));
            }
            else if (se.Status == (int)UposCashChangerStatusUpdateCode.Ok)
            {
                history.Add(new TransactionEntry(
                    DateTimeOffset.Now,
                    TransactionType.ErrorRecovery,
                    0,
                    new Dictionary<DenominationKey, int>()));
            }
        }
    }

    /// <summary>イベントの購読を解除します。</summary>
    public void Dispose()
    {
        device.OnEventQueued -= HandleDeviceEvent;
        GC.SuppressFinalize(this);
    }
}
