using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Coordination;
using CashChangerSimulator.UI.Wpf.Services;
using System.Runtime.CompilerServices;
using Moq;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Tests.UI.Wpf.Services;

public class DeviceFacadeTests
{
    [Fact]
    public void Constructor_WithValidArguments_ShouldSetProperties()
    {
        // Arrange
        var inventory = new Inventory();
        var manager = (CashChangerManager)RuntimeHelpers.GetUninitializedObject(typeof(CashChangerManager));
        var deposit = (DepositController)RuntimeHelpers.GetUninitializedObject(typeof(DepositController));
        var dispense = (DispenseController)RuntimeHelpers.GetUninitializedObject(typeof(DispenseController));
        var status = (HardwareStatusManager)RuntimeHelpers.GetUninitializedObject(typeof(HardwareStatusManager));
        var changer = (SimulatorCashChanger)RuntimeHelpers.GetUninitializedObject(typeof(SimulatorCashChanger));
        var history = (TransactionHistory)RuntimeHelpers.GetUninitializedObject(typeof(TransactionHistory));
        var aggregator = (OverallStatusAggregatorProvider)RuntimeHelpers.GetUninitializedObject(typeof(OverallStatusAggregatorProvider));
        var monitors = (MonitorsProvider)RuntimeHelpers.GetUninitializedObject(typeof(MonitorsProvider));
        var notify = new Mock<INotifyService>().Object;

        // Act
        var facade = new DeviceFacade(
            inventory,
            manager,
            deposit,
            dispense,
            status,
            changer,
            history,
            aggregator,
            monitors,
            notify);

        // Assert
        facade.Inventory.ShouldBeSameAs(inventory);
        facade.Manager.ShouldBeSameAs(manager);
        facade.Deposit.ShouldBeSameAs(deposit);
        facade.Dispense.ShouldBeSameAs(dispense);
        facade.Status.ShouldBeSameAs(status);
        facade.Changer.ShouldBeSameAs(changer);
        facade.History.ShouldBeSameAs(history);
        facade.AggregatorProvider.ShouldBeSameAs(aggregator);
        facade.Monitors.ShouldBeSameAs(monitors);
        facade.Notify.ShouldBeSameAs(notify);
    }

    [Fact]
    public void Constructor_WithNullArgument_ShouldThrowArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => new DeviceFacade(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!));
    }
}
