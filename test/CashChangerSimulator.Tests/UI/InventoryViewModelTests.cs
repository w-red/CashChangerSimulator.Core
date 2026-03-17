using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Coordination;
using CashChangerSimulator.UI.Wpf.Services;
using CashChangerSimulator.UI.Wpf.ViewModels;
using Moq;
using R3;
using Shouldly;

namespace CashChangerSimulator.Tests.UI;

/// <summary>Test class for providing InventoryViewModelTests functionality.</summary>
public class InventoryViewModelTests
{
    private static (InventoryViewModel vm, Inventory inv, ConfigurationProvider config, TransactionHistory history) CreateViewModel()
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
        var aggregatorProvider = new OverallStatusAggregatorProvider(monitorsProvider);
        var hw = new HardwareStatusManager();
        var sideSimulator = new Mock<IDeviceSimulator>();
        var dispenseController = new DispenseController(manager, hw, sideSimulator.Object);
        var depositController = new DepositController(inv, hw);
        var mockChanger = new Mock<InternalSimulatorCashChanger>(new SimulatorDependencies(
            config, inv, history, manager, depositController, dispenseController, aggregatorProvider, hw));
        
        mockChanger.Setup(x => x.Open()).Callback(() => 
            history.Add(new TransactionEntry(DateTimeOffset.Now, TransactionType.Open, 0, new Dictionary<DenominationKey, int>())));

        var notifyService = new Mock<INotifyService>().Object;
        
        var facade = new DeviceFacade(
            inv,
            manager,
            depositController,
            dispenseController,
            hw,
            mockChanger.Object,
            history,
            aggregatorProvider,
            monitorsProvider,
            notifyService,
            new Mock<IDispatcherService>().Object);

        var vm = new InventoryViewModel(facade, config, metadataProvider, notifyService);
        return (vm, inv, config, history);
    }

    /// <summary>Tests the behavior of CollectAllCommandShouldSetAllCountsToZero to ensure proper functionality.</summary>
    [Fact]
    public void CollectAllCommandShouldSetAllCountsToZero()
    {
        // Arrange
        var (vm, inv, _, _) = CreateViewModel();
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
        var (vm, inv, _, _) = CreateViewModel();
        var key100 = new DenominationKey(100, CurrencyCashType.Coin, "JPY");
        var key1000 = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");

        // Act
        vm.ReplenishAllCommand.Execute(Unit.Default);

        // Assert
        inv.GetCount(key100).ShouldBe(50); // InitialCount in CreateViewModel
        inv.GetCount(key1000).ShouldBe(20);
    }

    [Fact]
    public void OpenCommandShouldAddOpenEntryToHistory()
    {
        // Arrange
        var (vm, _, _, _) = CreateViewModel();
        vm.RecentTransactions.Clear();

        // Act
        vm.OpenCommand.Execute(Unit.Default);

        // Assert
        vm.RecentTransactions.Count.ShouldBe(1);
        vm.RecentTransactions[0].Type.ShouldBe(TransactionType.Open);
    }

    [Fact]
    public void DepositShouldAddEntryToHistory()
    {
        // Arrange
        var (vm, inv, config, history) = CreateViewModel();
        vm.RecentTransactions.Clear();
        var manager = new CashChangerManager(inv, history, new ChangeCalculator(), config);
        var key100 = new DenominationKey(100, CurrencyCashType.Coin, "JPY");

        // Act
        manager.Deposit(new Dictionary<DenominationKey, int> { [key100] = 1 });

        // Assert
        vm.RecentTransactions.Count.ShouldBe(1);
        vm.RecentTransactions[0].Type.ShouldBe(TransactionType.Deposit);
        vm.RecentTransactions[0].Amount.ShouldBe(100);
    }
}
