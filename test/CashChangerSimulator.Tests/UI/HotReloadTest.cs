using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using CashChangerSimulator.UI.Wpf.ViewModels;
using Moq;
using R3;
using Shouldly;
using System.Windows;
using Xunit;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Tests.UI;

public class HotReloadTest
{
    private (InventoryViewModel vm, ConfigurationProvider config, MonitorsProvider monitorsProvider) CreateSetup(string initialCurrency = "JPY")
    {
        var config = new ConfigurationProvider();
        config.Config.System.CurrencyCode = initialCurrency;

        var inv = new Inventory();
        var history = new TransactionHistory();
        var metadataProvider = new CurrencyMetadataProvider(config);
        var monitorsProvider = new MonitorsProvider(inv, config, metadataProvider);
        var aggregator = new OverallStatusAggregator(monitorsProvider.Monitors);
        var hw = new HardwareStatusManager();
        var depositController = new DepositController(inv, hw);
        var mockChanger = new Mock<SimulatorCashChanger>(config, inv, history, null!, null!, null!, null!, hw);
        var notifyService = new Mock<INotifyService>().Object;

        var vm = new InventoryViewModel(
            inv,
            history,
            aggregator,
            config,
            monitorsProvider,
            metadataProvider,
            hw,
            depositController,
            mockChanger.Object,
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
