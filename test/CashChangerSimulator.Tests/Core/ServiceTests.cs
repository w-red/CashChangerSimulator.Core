using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using Moq;
using R3;
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

    [Fact]
    public void RefreshMonitors_ShouldFallbackToGlobal_WhenSpecificCurrencyNotFound()
    {
        var inv = new Inventory();
        var configProvider = new ConfigurationProvider();
        var metadata = new CurrencyMetadataProvider(configProvider);
        
        // Set an unknown currency
        configProvider.Config.System.CurrencyCode = "USD";
        configProvider.Config.Thresholds.NearEmpty = 123;
        
        var provider = new MonitorsProvider(inv, configProvider, metadata);
        var monitor = provider.Monitors.First(m => m.Key.Value == 1000);
        
        // Should use global threshold since "USD" isn't in config.Inventory
        monitor.NearEmptyThreshold.ShouldBe(123);
    }

    [Fact]
    public void TriggerChanged_ShouldNotifyObservers()
    {
        var inv = new Inventory();
        var configProvider = new ConfigurationProvider();
        var metadata = new CurrencyMetadataProvider(configProvider);
        var provider = new MonitorsProvider(inv, configProvider, metadata);
        var called = false;
        
        provider.Changed.Subscribe(_ => called = true);
        provider.TriggerChanged();
        
        called.ShouldBeTrue();
    }

    [Fact]
    public void Dispose_ShouldClearMonitors()
    {
        var inv = new Inventory();
        var configProvider = new ConfigurationProvider();
        var metadata = new CurrencyMetadataProvider(configProvider);
        var provider = new MonitorsProvider(inv, configProvider, metadata);
        
        provider.Monitors.ShouldNotBeEmpty();
        provider.Dispose();
        provider.Monitors.ShouldBeEmpty();
    }

    [Fact]
    public void Reload_ShouldRefreshMonitors()
    {
        var inv = new Inventory();
        var configProvider = new ConfigurationProvider();
        var metadata = new CurrencyMetadataProvider(configProvider);
        var provider = new MonitorsProvider(inv, configProvider, metadata);
        
        var newConfig = new SimulatorConfiguration();
        newConfig.Inventory.Clear(); // Clear defaults to ensure fallback to global Thresholds
        newConfig.Thresholds.NearEmpty = 555;
        configProvider.Update(newConfig);
        
        provider.Monitors.First(m => m.Key.Value == 1000).NearEmptyThreshold.ShouldBe(555);
    }

    [Fact]
    public void MetadataChange_ShouldRefreshMonitors()
    {
        var inv = new Inventory();
        var configProvider = new ConfigurationProvider();
        var metadata = new Mock<ICurrencyMetadataProvider>();
        metadata.Setup(m => m.SupportedDenominations).Returns([new DenominationKey(100, CurrencyCashType.Coin)]);
        metadata.Setup(m => m.Changed).Returns(new Subject<Unit>());
        
        var provider = new MonitorsProvider(inv, configProvider, metadata.Object);
        provider.Monitors.Count.ShouldBe(1);

        // Update mock and trigger change
        metadata.Setup(m => m.SupportedDenominations).Returns([new DenominationKey(100, CurrencyCashType.Coin), new DenominationKey(500, CurrencyCashType.Coin)]);
        ((Subject<Unit>)metadata.Object.Changed).OnNext(Unit.Default);

        provider.Monitors.Count.ShouldBe(2);
    }
}
