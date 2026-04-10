using System.Diagnostics.CodeAnalysis;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using R3;

namespace CashChangerSimulator.Core.Services;

/// <summary>全金種の CashStatusMonitor インスタンスを提供するプロバイダー。</summary>
public class MonitorsProvider : IDisposable
{
    private readonly Inventory inventory;
    private readonly ConfigurationProvider configProvider;
    private readonly ICurrencyMetadataProvider metadataProvider;
    private readonly CompositeDisposable disposables = [];
    private List<CashStatusMonitor> monitors = [];

    /// <summary>Initializes a new instance of the <see cref="MonitorsProvider"/> class.在庫と設定を元に、全金種のモニターを初期化する。</summary>
    /// <param name="inventory">在庫マネージャー。</param>
    /// <param name="configProvider">設定プロバイダー。</param>
    /// <param name="metadataProvider">通貨メタデータプロバイダー。</param>
    private MonitorsProvider(Inventory inventory, ConfigurationProvider configProvider, ICurrencyMetadataProvider metadataProvider)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(configProvider);
        ArgumentNullException.ThrowIfNull(metadataProvider);

        this.inventory = inventory;
        this.configProvider = configProvider;
        this.metadataProvider = metadataProvider;

        var changedSubject = new Subject<Unit>();
        disposables.Add(changedSubject);
        Changed = changedSubject;

        RefreshMonitors();

        // 構成変更時またはメタデータ変更時（通貨変更時など）にモニターリストも更新する
        configProvider.Reloaded.Subscribe(_ => RefreshMonitors()).AddTo(disposables);
        metadataProvider.Changed.Subscribe(_ => RefreshMonitors()).AddTo(disposables);
    }

    /// <summary>生成されたモニターのリスト。</summary>
    public IReadOnlyList<CashStatusMonitor> Monitors => monitors;

    /// <summary>モニターリストが変更されたときに通知されるストリーム。</summary>
    public Observable<Unit> Changed { get; }

    /// <summary>在庫と設定を元に、全金種のモニタープロバイダーを生成・初期化します。</summary>
    /// <param name="inventory">在庫マネージャー。</param>
    /// <param name="configProvider">設定プロバイダー。</param>
    /// <param name="metadataProvider">通貨メタデータプロバイダー。</param>
    /// <returns>初期化済みの <see cref="MonitorsProvider"/> インスタンス。</returns>
    public static MonitorsProvider Create(Inventory inventory, ConfigurationProvider configProvider, ICurrencyMetadataProvider metadataProvider)
    {
        return new MonitorsProvider(inventory, configProvider, metadataProvider);
    }

    /// <summary>現在の通貨設定に基づいてモニターリストを再構築する。</summary>
    public void RefreshMonitors()
    {
        var config = configProvider.Config;
        var keys = metadataProvider.SupportedDenominations;

        // Dispose existing monitors before refreshing
        foreach (var monitor in monitors)
        {
            monitor.Dispose();
        }

        monitors = keys.Select(k =>
        {
            var activeCurrency = config.System.CurrencyCode ?? "JPY";
            var keyStr = k.ToDenominationString();

            if (config.Inventory.TryGetValue(activeCurrency, out var inventorySettings) &&
                inventorySettings.Denominations.TryGetValue(keyStr, out var setting))
            {
                return new CashStatusMonitor(
                    inventory,
                    k,
                    setting.IsRecyclable ? setting.NearEmpty : -1,
                    setting.IsRecyclable ? setting.NearFull : -1,
                    setting.IsRecyclable ? setting.Full : -1,
                    setting.IsRecyclable);
            }

            var globalSetting = config.GetDenominationSetting(k);
            return new CashStatusMonitor(
                inventory,
                k,
                globalSetting.IsRecyclable ? config.Thresholds.NearEmpty : -1,
                globalSetting.IsRecyclable ? config.Thresholds.NearFull : -1,
                globalSetting.IsRecyclable ? config.Thresholds.Full : -1,
                globalSetting.IsRecyclable);
        }).ToList();
        ((Subject<Unit>)Changed).OnNext(Unit.Default);
    }

    /// <summary>設定オブジェクトを元に、全モニターのしきい値を更新する（ホットリロード用）。</summary>
    /// <param name="config">更新に使用する設定オブジェクト。</param>
    public void UpdateThresholdsFromConfig(SimulatorConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var activeCurrency = config.System.CurrencyCode ?? "JPY";
        foreach (var monitor in Monitors)
        {
            var k = monitor.Key;
            var keyStr = k.ToDenominationString();

            if (config.Inventory.TryGetValue(activeCurrency, out var inventorySettings) &&
                inventorySettings.Denominations.TryGetValue(keyStr, out var setting))
            {
                monitor.UpdateThresholds(
                    setting.IsRecyclable ? setting.NearEmpty : -1,
                    setting.IsRecyclable ? setting.NearFull : -1,
                    setting.IsRecyclable ? setting.Full : -1);
            }
            else
            {
                var globalSetting = config.GetDenominationSetting(k);
                monitor.UpdateThresholds(
                    globalSetting.IsRecyclable ? config.Thresholds.NearEmpty : -1,
                    globalSetting.IsRecyclable ? config.Thresholds.NearFull : -1,
                    globalSetting.IsRecyclable ? config.Thresholds.Full : -1);
            }
        }
    }

    /// <summary>テスト用：手動で変更通知を発火させます。</summary>
    public void TriggerChanged() => ((Subject<Unit>)Changed).OnNext(Unit.Default);

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>リソースを解放します。</summary>
    /// <param name="disposing">明示的な破棄かどうか。</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            disposables.Dispose();
            foreach (var monitor in monitors)
            {
                monitor.Dispose();
            }

            monitors.Clear();
        }
    }
}
