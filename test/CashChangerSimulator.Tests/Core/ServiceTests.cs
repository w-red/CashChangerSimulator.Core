using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using Shouldly;

namespace CashChangerSimulator.Tests.Core;

public class MonitorsProviderTests
{
    [Fact]
    public void UpdateThresholdsFromConfig_ShouldUpdateCorrectly()
    {
        var inv = new Inventory();
        var configProvider = new ConfigurationProvider();
        var metadata = new CurrencyMetadataProvider(configProvider);
        var provider = new MonitorsProvider(inv, configProvider, metadata);

        var monitor = provider.Monitors.First(m => m.Key.Value == 1000);
        monitor.NearEmptyThreshold.ShouldBe(configProvider.Config.Thresholds.NearEmpty);

        var newConfig = new SimulatorConfiguration();
        // Specific setting overrides global Thresholds
        newConfig.Inventory["JPY"].Denominations["B1000"].NearEmpty = 99;
        
        provider.UpdateThresholdsFromConfig(newConfig);
        monitor.NearEmptyThreshold.ShouldBe(99);
    }

    [Fact]
    public void RefreshMonitors_ShouldHandleNonRecyclable()
    {
        var inv = new Inventory();
        var configProvider = new ConfigurationProvider();
        var metadata = new CurrencyMetadataProvider(configProvider);
        
        // Disable recycling for 2000 Yen in config
        configProvider.Config.Inventory["JPY"].Denominations["B2000"].IsRecyclable = false;
        
        var provider = new MonitorsProvider(inv, configProvider, metadata);
        var monitor2000 = provider.Monitors.First(m => m.Key.Value == 2000);
        
        monitor2000.NearEmptyThreshold.ShouldBe(-1);
        monitor2000.FullThreshold.ShouldBe(-1);
    }
}
