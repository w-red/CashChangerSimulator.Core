using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using CashChangerSimulator.UI.Wpf.ViewModels;
using R3;
using Shouldly;

namespace CashChangerSimulator.Tests.UI;

/// <summary>Test class for providing InventoryViewModelTests functionality.</summary>
public class InventoryViewModelTests
{
    private (InventoryViewModel vm, Inventory inv, ConfigurationProvider config) CreateViewModel()
    {
        var config = new ConfigurationProvider();
        // Setup some initial counts in config
        config.Config.Inventory["JPY"] = new InventorySettings
        {
            Denominations = new()
            {
                ["C100"] = new() { InitialCount = 50 },
                ["B1000"] = new() { InitialCount = 20 }
            }
        };

        var inv = new Inventory();
        var key100 = new DenominationKey(100, CurrencyCashType.Coin, "JPY");
        var key1000 = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        inv.SetCount(key100, 10);
        inv.SetCount(key1000, 5);

        var history = new TransactionHistory();
        var manager = new CashChangerManager(inv, history, new ChangeCalculator());
        var metadataProvider = new CurrencyMetadataProvider(config);
        var monitorsProvider = new MonitorsProvider(inv, config, metadataProvider);
        var aggregator = new OverallStatusAggregator(monitorsProvider.Monitors);
        var hw = new HardwareStatusManager();
        var depositController = new DepositController(inv, hw);
        var mockChanger = new Moq.Mock<InternalSimulatorCashChanger>();

        var notifyService = new Moq.Mock<INotifyService>().Object;
        var vm = new InventoryViewModel(inv, history, aggregator, config, monitorsProvider, metadataProvider, hw, depositController, mockChanger.Object, notifyService);
        return (vm, inv, config);
    }

    /// <summary>Tests the behavior of CollectAllCommandShouldSetAllCountsToZero to ensure proper functionality.</summary>
    [Fact]
    public void CollectAllCommandShouldSetAllCountsToZero()
    {
        // Arrange
        var (vm, inv, config) = CreateViewModel();
        var key100 = new DenominationKey(100, CurrencyCashType.Coin, "JPY");
        var key1000 = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");

        // Act
        vm.CollectAllCommand.Execute(Unit.Default);

        // Assert
        inv.GetCount(key100).ShouldBe(0);
        inv.GetCount(key1000).ShouldBe(0);
    }

    /// <summary>Tests the behavior of ReplenishAllCommandShouldSetCountsToInitialValues to ensure proper functionality.</summary>
    [Fact]
    public void ReplenishAllCommandShouldSetCountsToInitialValues()
    {
        // Arrange
        var (vm, inv, config) = CreateViewModel();
        var key100 = new DenominationKey(100, CurrencyCashType.Coin, "JPY");
        var key1000 = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");

        // Act
        vm.ReplenishAllCommand.Execute(Unit.Default);

        // Assert
        inv.GetCount(key100).ShouldBe(50); // InitialCount in CreateViewModel
        inv.GetCount(key1000).ShouldBe(20);
    }
}
