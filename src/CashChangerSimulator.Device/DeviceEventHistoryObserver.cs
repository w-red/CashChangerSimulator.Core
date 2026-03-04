using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Transactions;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device;

/// <summary>
/// UPOSデバイス (SimulatorCashChanger) から発火されるイベントを購読し、
/// UIのActivity Feed等に表示するための取引履歴 (TransactionHistory) を記録するオブザーバー層。
/// デバイス層が上位のUI履歴要件に依存しないように隔離するためのクラスです。
/// </summary>
public class DeviceEventHistoryObserver : IDisposable
{
    private readonly SimulatorCashChanger _device;
    private readonly TransactionHistory _history;

    /// <summary>オブザーバーを初期化し、イベントを購読します。</summary>
    public DeviceEventHistoryObserver(SimulatorCashChanger device, TransactionHistory history)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _history = history ?? throw new ArgumentNullException(nameof(history));

        _device.OnEventQueued += HandleDeviceEvent;
    }

    private void HandleDeviceEvent(EventArgs e)
    {
        if (e is DataEventArgs)
        {
            _history.Add(new TransactionEntry(
                DateTimeOffset.Now,
                TransactionType.DataEvent,
                0,
                new Dictionary<DenominationKey, int>()
            ));
        }
    }

    /// <summary>イベントの購読を解除します。</summary>
    public void Dispose()
    {
        _device.OnEventQueued -= HandleDeviceEvent;
    }
}
