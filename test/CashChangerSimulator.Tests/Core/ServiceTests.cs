using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using Moq;
using R3;
using Shouldly;

namespace CashChangerSimulator.Tests.Core;

/// <summary>MonitorsProvider の金種モニター管理およびしきい値更新機能を検証するテストクラス。</summary>
public class MonitorsProviderTests
{
    /// <summary>設定（Configuration）の変更が、各モニターのしきい値へ正しく反映されることを検証します。</summary>
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

    /// <summary>非還流金種（IsRecyclable=false）の設定時、モニターの監視が無効化（しきい値-1）されることを検証します。</summary>
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

    /// <summary>通貨個別の設定が見つからない場合に、グローバル設定のしきい値が使用されることを検証します。</summary>
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

    /// <summary>TriggerChanged 呼び出しにより、変更通知（Changed）が発火されることを検証します。</summary>
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

    /// <summary>Dispose 呼び出しにより、管理対象のモニターがクリアされることを検証します。</summary>
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

    /// <summary>設定のリロードにより、モニターのしきい値が再読み込みされることを検証します。</summary>
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

    /// <summary>通貨メタデータの変更に伴い、監視対象の金種キーリストが更新されることを検証します。</summary>
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
