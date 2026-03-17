using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
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
using Xunit;
using System.Linq;

namespace CashChangerSimulator.Tests.UI;

public class HotReloadTest
{
    private (InventoryViewModel vm, ConfigurationProvider config, MonitorsProvider monitorsProvider) CreateSetup(string initialCurrency = "JPY")
    {
        var config = new ConfigurationProvider();
        config.Config.System.CurrencyCode = initialCurrency;
        
        // Ensure JPY 2000 is not recyclable for this test to be deterministic regardless of local config.toml
        if (config.Config.Inventory.TryGetValue("JPY", out var jpySettings) && 
            jpySettings.Denominations.TryGetValue("B2000", out var b2000))
        {
            b2000.IsRecyclable = false;
        }

        var inv = new Inventory();
        var history = new TransactionHistory();
        var metadataProvider = new CurrencyMetadataProvider(config);
        var monitorsProvider = new MonitorsProvider(inv, config, metadataProvider);
        var aggregatorProvider = new OverallStatusAggregatorProvider(monitorsProvider);
        var hw = new HardwareStatusManager();
        var manager = new CashChangerManager(inv, history, new ChangeCalculator());
        var depositController = new DepositController(inv, hw);
        var dispenseController = new DispenseController(manager, hw, new Mock<IDeviceSimulator>().Object);
        var mockChanger = new Mock<SimulatorCashChanger>(new SimulatorDependencies(
            config, inv, history, manager, depositController, dispenseController, aggregatorProvider, hw));
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

        var vm = new InventoryViewModel(
            facade,
            config,
            metadataProvider,
            notifyService);

        return (vm, config, monitorsProvider);
    }

    [Fact]
    public void MonitorsProviderShouldRefreshOnReload()
    {
        // Arrange
        var (_, config, monitorsProvider) = CreateSetup("JPY");
        var initialMonitors = monitorsProvider.Monitors.ToList();
        initialMonitors.Any(m => m.Key.CurrencyCode == "JPY").ShouldBeTrue();

        // Act: Change currency and reload
        config.Config.System.CurrencyCode = "USD";
        config.Update(config.Config); // This triggers Reloaded event

        // Assert
        monitorsProvider.Monitors.Any(m => m.Key.CurrencyCode == "USD").ShouldBeTrue();
        monitorsProvider.Monitors.Any(m => m.Key.CurrencyCode == "JPY").ShouldBeFalse();
    }

    [Fact]
    public void InventoryViewModelShouldRefreshDenominationsOnReload()
    {
        // Arrange
        var (vm, config, _) = CreateSetup("JPY");
        vm.BillDenominations.Any(d => d.Key.CurrencyCode == "JPY").ShouldBeTrue();

        // Act
        config.Config.System.CurrencyCode = "USD";
        config.Update(config.Config);

        // Assert
        vm.BillDenominations.Any(d => d.Key.CurrencyCode == "USD").ShouldBeTrue();
        vm.BillDenominations.Any(d => d.Key.CurrencyCode == "JPY").ShouldBeFalse();
    }

    [Fact]
    public void GridRatiosShouldBeCalculatedBasedOnDenominationCount()
    {
        // Arrange
        var (vm, config, _) = CreateSetup("JPY");

        // JPY default: Bills(4: 10k, 5k, 2k, 1k), Coins(6: 500, 100, 50, 10, 5, 1)
        // Ratio should be 4:6
        vm.BillGridWidth.Value.Value.ShouldBe(4);
        vm.CoinGridWidth.Value.Value.ShouldBe(6);

        // Act: Switch to USD
        // USD default: Bills(6: 100, 50, 20, 10, 5, 1), Coins(6: 1, 0.5, 0.25, 0.1, 0.05, 0.01)
        // Ratio should be 6:6
        config.Config.System.CurrencyCode = "USD";
        config.Update(config.Config);

        // Assert
        vm.BillGridWidth.Value.Value.ShouldBe(6);
        vm.CoinGridWidth.Value.Value.ShouldBe(6);
    }
}

public class SimulatorOpenTest
{
    [Fact]
    public void InternalCashChangerShouldDefaultToSkipVerification()
    {
        // This test represents the "Red" state where InternalSimulatorCashChanger
        // might default to SkipStateVerification = false without environment variables.
        var changer = new InternalSimulatorCashChanger();
        changer.SkipStateVerification.ShouldBeTrue();
    }

    [Fact]
    public void OpenShouldBeIdempotent()
    {
        var changer = new InternalSimulatorCashChanger();
        changer.SkipStateVerification = true;
        
        // Act & Assert
        Should.NotThrow(() => 
        {
            changer.Open();
            changer.Open(); // Second call should not throw PosControlException
        });
    }

    [Fact]
    public void CloseShouldBeIdempotent()
    {
        var changer = new InternalSimulatorCashChanger();
        changer.SkipStateVerification = true;
        
        // Act & Assert
        Should.NotThrow(() => 
        {
            changer.Open();
            changer.Close();
            changer.Close(); // Second call should not throw
        });
    }

    [Fact]
    public void ReloadedShouldNotClearInventoryIfDeviceIsClosed()
    {
        var config = new ConfigurationProvider();
        var inv = new Inventory();
        var key = new DenominationKey(10000m, CurrencyCashType.Bill, "JPY");
        inv.SetCount(key, 10);
        
        // InternalSimulatorCashChanger handles the subscription in its base (SimulatorCashChanger)
        var changer = new InternalSimulatorCashChanger(configProvider: config, inventory: inv);
        
        // Act: Trigger reload while closed
        config.Update(config.Config);
        
        // Assert: Count should still be 10
        inv.GetCount(key).ShouldBe(10);
        
        // Act: Open and then reload
        changer.Open();
        config.Update(config.Config);
        
        // Assert: Now it should be cleared
        inv.GetCount(key).ShouldBe(0);
    }
}
