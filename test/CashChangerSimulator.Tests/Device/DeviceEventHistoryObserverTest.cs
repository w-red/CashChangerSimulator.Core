using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>
/// <see cref="DeviceEventHistoryObserver"/> の動作を検証するテストクラス。
/// デバイス層の DataEvent が TransactionHistory に委譲記述されるかをテストします。
/// </summary>
public class DeviceEventHistoryObserverTest
{
    /// <summary>DataEventArgs を伴うデバイスイベントが発生した際、履歴に追加されることを検証します。</summary>
    [Fact]
    public void HandleDeviceEvent_WhenDataEventArgs_RecordsToHistory()
    {
        // Arrange
        var history = new TransactionHistory();
        var device = new InternalSimulatorCashChanger(history: history);

        // This instantiates the observer, subscribing to the device
        using var observer = new DeviceEventHistoryObserver(device, history);

        // Act
        // Typically DataEvents are queued internally, but we can trigger the action here
        // to simulate the device firing the event.
        device.OnEventQueued?.Invoke(new DataEventArgs(0));

        // Assert
        history.Entries.Count.ShouldBe(1);
        var entry = history.Entries[0];
        entry.Type.ShouldBe(TransactionType.DataEvent);
        entry.Amount.ShouldBe(0);
        entry.Counts.ShouldBeEmpty();
    }

    /// <summary>DataEventArgs 以外のデバイスイベントが発生した際、履歴に追加「されない」ことを検証します。</summary>
    [Fact]
    public void HandleDeviceEvent_WhenNotDataEventArgs_DoesNotRecordToHistory()
    {
        // Arrange
        var history = new TransactionHistory();
        var device = new InternalSimulatorCashChanger(history: history);
        using var observer = new DeviceEventHistoryObserver(device, history);

        // Act
        device.OnEventQueued?.Invoke(new StatusUpdateEventArgs(0));

        // Assert
        history.Entries.ShouldBeEmpty();
    }
}
