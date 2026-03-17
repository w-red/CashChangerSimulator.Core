using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Coordination;
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
    public void HandleDeviceEventWhenDataEventArgsRecordsToHistory()
    {
        // Arrange
        var history = new TransactionHistory();
        var deps = new SimulatorDependencies(History: history);
        var device = new InternalSimulatorCashChanger(deps);

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

    /// <summary>StatusUpdateEventArgs(Ok=0) が ErrorRecovery として履歴に記録されることを検証します。</summary>
    [Fact]
    public void HandleDeviceEventWhenStatusOkRecordsErrorRecovery()
    {
        // Arrange
        var history = new TransactionHistory();
        var deps = new SimulatorDependencies(History: history);
        var device = new InternalSimulatorCashChanger(deps);
        using var observer = new DeviceEventHistoryObserver(device, history);

        // Act — StatusUpdateEventArgs(0) corresponds to UposCashChangerStatusUpdateCode.Ok
        device.OnEventQueued?.Invoke(new StatusUpdateEventArgs(0));

        // Assert
        history.Entries.Count.ShouldBe(1);
        history.Entries[0].Type.ShouldBe(TransactionType.ErrorRecovery);
    }

    /// <summary>StatusUpdateEventArgs(Jam=31) が HardwareError として履歴に記録されることを検証します。</summary>
    [Fact]
    public void HandleDeviceEventWhenStatusJamRecordsHardwareError()
    {
        // Arrange
        var history = new TransactionHistory();
        var deps = new SimulatorDependencies(History: history);
        var device = new InternalSimulatorCashChanger(deps);
        using var observer = new DeviceEventHistoryObserver(device, history);

        // Act — StatusUpdateEventArgs(31) corresponds to UposCashChangerStatusUpdateCode.Jam
        device.OnEventQueued?.Invoke(new StatusUpdateEventArgs(31));

        // Assert
        history.Entries.Count.ShouldBe(1);
        history.Entries[0].Type.ShouldBe(TransactionType.HardwareError);
    }

    /// <summary>未対応のステータスコードではエントリが追加されないことを検証します。</summary>
    [Fact]
    public void HandleDeviceEventWhenUnhandledStatusDoesNotRecord()
    {
        // Arrange
        var history = new TransactionHistory();
        var deps = new SimulatorDependencies(History: history);
        var device = new InternalSimulatorCashChanger(deps);
        using var observer = new DeviceEventHistoryObserver(device, history);

        // Act — Use a status code that is NOT Ok (0) or Jam (31)
        device.OnEventQueued?.Invoke(new StatusUpdateEventArgs(11)); // Empty

        // Assert
        history.Entries.ShouldBeEmpty();
    }
}
