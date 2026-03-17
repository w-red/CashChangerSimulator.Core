using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Coordination;
using CashChangerSimulator.UI.Wpf.Services;
using CashChangerSimulator.UI.Wpf.ViewModels;
using CashChangerSimulator.Tests.Mocks;
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
        config.Config.Inventory[TestConstants.DefaultCurrency] = new InventorySettings
        {
            Denominations = new()
            {
                ["C100"] = new() { InitialCount = TestConstants.ConfigCount100 },
                ["B1000"] = new() { InitialCount = TestConstants.ConfigCount1000 }
            }
        };

        var inv = new Inventory();
        inv.SetCount(TestConstants.Key100, TestConstants.StartCount100);
        inv.SetCount(TestConstants.Key1000, TestConstants.StartCount1000);

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
            new ImmediateDispatcherService(),
            new ImmediateViewService());

        var vm = new InventoryViewModel(facade, config, metadataProvider, notifyService);
        return (vm, inv, config, history);
    }

    /// <summary>Tests the behavior of CollectAllCommandShouldSetAllCountsToZero to ensure proper functionality.</summary>
    [Fact]
    public void CollectAllCommandShouldSetAllCountsToZero()
    {
        // Arrange
        var (vm, inv, _, _) = CreateViewModel();
 
        // Act
        vm.CollectAllCommand.Execute(Unit.Default);
 
        // Assert
        inv.GetCount(TestConstants.Key100).ShouldBe(0);
        inv.GetCount(TestConstants.Key1000).ShouldBe(0);
    }

    /// <summary>Tests the behavior of ReplenishAllCommandShouldSetCountsToInitialValues to ensure proper functionality.</summary>
    [Fact]
    public void ReplenishAllCommandShouldSetCountsToInitialValues()
    {
        // Arrange
        var (vm, inv, _, _) = CreateViewModel();
 
        // Act
        vm.ReplenishAllCommand.Execute(Unit.Default);
 
        // Assert
        inv.GetCount(TestConstants.Key100).ShouldBe(TestConstants.ConfigCount100);
        inv.GetCount(TestConstants.Key1000).ShouldBe(TestConstants.ConfigCount1000);
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
 
        // Act
        manager.Deposit(new Dictionary<DenominationKey, int> { [TestConstants.Key100] = 1 });
 
        // Assert
        vm.RecentTransactions.Count.ShouldBe(1);
        vm.RecentTransactions[0].Type.ShouldBe(TransactionType.Deposit);
        vm.RecentTransactions[0].Amount.ShouldBe(100);
    }
}
