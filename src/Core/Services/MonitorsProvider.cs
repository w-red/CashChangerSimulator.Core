using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using R3;

namespace CashChangerSimulator.Core.Services;

/// <summary>全金種の CashStatusMonitor インスタンスを提供するプロバイダー。</summary>
public class MonitorsProvider
{
    private readonly Inventory _inventory;
    private readonly ConfigurationProvider _configProvider;
    private readonly ICurrencyMetadataProvider _metadataProvider;
    private List<CashStatusMonitor> _monitors = [];
    private readonly Subject<Unit> _changed = new();
    private readonly CompositeDisposable _disposables = [];

    /// <summary>生成されたモニターのリスト。</summary>
    public IReadOnlyList<CashStatusMonitor> Monitors => _monitors;

    /// <summary>モニターリストが変更されたときに通知されるストリーム。</summary>
    public Observable<Unit> Changed => _changed;

    /// <summary>在庫と設定を元に、全金種のモニターを初期化する。</summary>
    public MonitorsProvider(Inventory inventory, ConfigurationProvider configProvider, ICurrencyMetadataProvider metadataProvider)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(configProvider);
        ArgumentNullException.ThrowIfNull(metadataProvider);

        _inventory = inventory;
        _configProvider = configProvider;
        _metadataProvider = metadataProvider;
        
        RefreshMonitors();

        // 構成変更時またはメタデータ変更時（通貨変更時など）にモニターリストも更新する
        _configProvider.Reloaded.Subscribe(_ => RefreshMonitors()).AddTo(_disposables);
        _metadataProvider.Changed.Subscribe(_ => RefreshMonitors()).AddTo(_disposables);
    }

    /// <summary>現在の通貨設定に基づいてモニターリストを再構築する。</summary>
    public void RefreshMonitors()
    {
        var config = _configProvider.Config;
        var keys = _metadataProvider.SupportedDenominations;

        _monitors = keys.Select(k =>
        {
            var activeCurrency = config.System.CurrencyCode ?? "JPY";
            var keyStr = k.ToDenominationString();

            if (config.Inventory.TryGetValue(activeCurrency, out var inventorySettings) &&
                inventorySettings.Denominations.TryGetValue(keyStr, out var setting))
            {
                return new CashStatusMonitor(
                    _inventory,
                    k,
                    setting.IsRecyclable ? setting.NearEmpty : -1,
                    setting.IsRecyclable ? setting.NearFull : -1,
                    setting.IsRecyclable ? setting.Full : -1,
                    setting.IsRecyclable);
            }

            var globalSetting = config.GetDenominationSetting(k);
            return new CashStatusMonitor(
                _inventory,
                k,
                globalSetting.IsRecyclable ? config.Thresholds.NearEmpty : -1,
                globalSetting.IsRecyclable ? config.Thresholds.NearFull : -1,
                globalSetting.IsRecyclable ? config.Thresholds.Full : -1,
                globalSetting.IsRecyclable);
        }).ToList();
        _changed.OnNext(Unit.Default);
    }

    /// <summary>設定オブジェクトを元に、全モニターのしきい値を更新する（ホットリロード用）。</summary>
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
    public void TriggerChanged() => _changed.OnNext(Unit.Default);

    /// <summary>リソースを解放します。</summary>
    public void Dispose()
    {
        _disposables.Dispose();
        _changed.Dispose();
        foreach (var monitor in _monitors)
        {
            monitor.Dispose();
        }
        _monitors.Clear();
        GC.SuppressFinalize(this);
    }
}
