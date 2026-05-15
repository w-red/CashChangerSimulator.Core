using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using Moq;
using R3;
using Shouldly;

namespace CashChangerSimulator.Tests.Core.Services;

/// <summary>MonitorsProvider の金種モニター管理およびしきい値更新機能を検証するテストクラス。</summary>
public class MonitorsProviderTests : IDisposable
{
    private readonly Inventory inv;
    private readonly ConfigurationProvider configProvider;
    private readonly CurrencyMetadataProvider metadata;
    private readonly MonitorsProvider provider;

    public MonitorsProviderTests()
    {
        inv = Inventory.Create();
        configProvider = new ConfigurationProvider(false);
        metadata = CurrencyMetadataProvider.Create(configProvider);
        provider = MonitorsProvider.Create(inv, configProvider, metadata);
    }

    /// <summary>設定(Configuration)の変更が、各モニターのしきい値へ正しく反映されることを検証します。</summary>
    [Fact]
    public void UpdateThresholdsFromConfigShouldUpdateCorrectly()
    {
        var monitor2000 = provider.Monitors.First(m => m.Key.Value == 2000);
        monitor2000.NearEmptyThreshold.ShouldBe(-1);
        monitor2000.FullThreshold.ShouldBe(-1);
        monitor2000.NearFullThreshold.ShouldBe(-1);

        var newConfig = new SimulatorConfiguration();

        // Specific setting overrides global Thresholds
        newConfig.Inventory["JPY"].Denominations["B1000"].NearEmpty = 99;

        provider.UpdateThresholdsFromConfig(newConfig);
        var monitor = provider.Monitors.First(m => m.Key.Value == 1000);
        monitor.NearEmptyThreshold.ShouldBe(99);
    }

    /// <summary>非還流金種(IsRecyclable=false)の設定時、モニターの監視が無効化(しきい値-1)されることを検証します。</summary>
    [Fact]
    public void RefreshMonitorsShouldHandleNonRecyclable()
    {
        // Disable recycling for 2000 Yen in config
        configProvider.Config.Inventory["JPY"].Denominations["B2000"].IsRecyclable = false;

        provider.RefreshMonitors();
        var monitor2000 = provider.Monitors.First(m => m.Key.Value == 2000);

        // ID 1147 対策: NearEmpty, Full, NearFull すべてが -1 であることを厳密に検証
        monitor2000.NearEmptyThreshold.ShouldBe(-1);
        monitor2000.NearFullThreshold.ShouldBe(-1);
        monitor2000.FullThreshold.ShouldBe(-1);
    }

    [Fact]
    public void CreateShouldReturnInstanceWithMonitors()
    {
        var instance = MonitorsProvider.Create(inv, configProvider, metadata);
        instance.ShouldNotBeNull();
        instance.Monitors.ShouldNotBeEmpty();
    }

    /// <summary>通貨個別の設定が見つからない場合に、グローバル設定のしきい値が使用されることを検証します。</summary>
    [Fact]
    public void RefreshMonitorsShouldFallbackToGlobalWhenSpecificCurrencyNotFound()
    {
        // Set an unknown currency
        configProvider.Config.System.CurrencyCode = "USD";
        configProvider.Config.Thresholds.NearEmpty = 123;

        provider.RefreshMonitors();
        var monitor = provider.Monitors.First(m => m.Key.Value == 1000);

        // Should use global threshold since "USD" isn't in config.Inventory
        monitor.NearEmptyThreshold.ShouldBe(123);
    }

    /// <summary>TriggerChanged 呼び出しにより、変更通知(Changed)が発火されることを検証します。</summary>
    [Fact]
    public void TriggerChangedShouldNotifyObservers()
    {
        var called = false;
        provider.Changed.Subscribe(_ => called = true);
        provider.TriggerChanged();

        called.ShouldBeTrue();
    }

    /// <summary>Dispose 呼び出しにより、管理対象のモニターがクリアされることを検証します。</summary>
    [Fact]
    public void DisposeShouldClearMonitors()
    {
        provider.Monitors.ShouldNotBeEmpty();
        provider.Dispose();
        provider.Monitors.ShouldBeEmpty();
    }

    /// <summary>設定のリロードにより、モニターのしきい値が再読み込みされることを検証します。</summary>
    [Fact]
    public void ReloadShouldRefreshMonitors()
    {
        var newConfig = new SimulatorConfiguration();
        newConfig.Inventory.Clear(); // Clear defaults to ensure fallback to global Thresholds
        newConfig.Thresholds.NearEmpty = 555;
        configProvider.Update(newConfig);

        provider.Monitors.First(m => m.Key.Value == 1000).NearEmptyThreshold.ShouldBe(555);
    }

    /// <summary>通貨メタデータの変更に伴い、監視対象の金種キーリストが更新されることを検証します。</summary>
    [Fact]
    public void MetadataChangeShouldRefreshMonitors()
    {
        var metadataMock = new Mock<ICurrencyMetadataProvider>();
        metadataMock.Setup(m => m.SupportedDenominations).Returns([new DenominationKey(100, CurrencyCashType.Coin)]);
        metadataMock.Setup(m => m.Changed).Returns(new Subject<Unit>());

        var localProvider = MonitorsProvider.Create(inv, configProvider, metadataMock.Object);
        localProvider.Monitors.Count.ShouldBe(1);

        // Update mock and trigger change
        metadataMock.Setup(m => m.SupportedDenominations).Returns([new DenominationKey(100, CurrencyCashType.Coin), new DenominationKey(500, CurrencyCashType.Coin)]);
        ((Subject<Unit>)metadataMock.Object.Changed).OnNext(Unit.Default);

        localProvider.Monitors.Count.ShouldBe(2);
    }

    /// <summary>フォールバック時の非還流設定(-1)が正しく適用されることを検証します(L130-132)。</summary>
    [Fact]
    public void UpdateThresholdsFromConfigShouldHandleFallbackToGlobalNonRecyclable()
    {
        // 1. USD をサポート金種に含める
        configProvider.Config.System.CurrencyCode = "USD";
        configProvider.Update(configProvider.Config);
        provider.RefreshMonitors();

        var usdMonitor = provider.Monitors.First(m => m.Key.CurrencyCode == "USD" && m.Key.Value == 100);

        // 2. アクティブ通貨を JPY に変え、かつ USD:B100 を非還流に設定した新構成を作成
        var newConfig = new SimulatorConfiguration();
        newConfig.System.CurrencyCode = "JPY";
        newConfig.Inventory["USD"].Denominations["B100"].IsRecyclable = false;

        // 3. 更新実行。JPYの設定には USD:B100 はないのでフォールバックが発生し、USDの設定から IsRecyclable=false が読まれるはず
        provider.UpdateThresholdsFromConfig(newConfig);

        usdMonitor.NearEmptyThreshold.ShouldBe(-1);
        usdMonitor.FullThreshold.ShouldBe(-1);
    }

    public void Dispose()
    {
        provider.Dispose();
        configProvider.Dispose();
        inv.Dispose();
        metadata.Dispose();
        GC.SuppressFinalize(this);
    }
}
