using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;

namespace CashChangerSimulator.Core.Services;

/// <summary>全金種の CashStatusMonitor インスタンスを提供するプロバイダー。</summary>
public class MonitorsProvider
{
    /// <summary>生成されたモニターのリスト。</summary>
    public IReadOnlyList<CashStatusMonitor> Monitors { get; }

    /// <summary>在庫と設定を元に、全金種のモニターを初期化する。</summary>
    public MonitorsProvider(Inventory inventory, ConfigurationProvider configProvider, CurrencyMetadataProvider metadataProvider)
    {
        var config = configProvider.Config;
        var keys = metadataProvider.SupportedDenominations;

        Monitors = keys.Select(k =>
        {
            var activeCurrency = config.System.CurrencyCode ?? "JPY";
            // 金種キーを文字列に変換 (B1000, C100 等)
            var keyStr = (k.Type == MoneyKind4Opos.Currencies.Interfaces.CashType.Bill ? "B" : "C") + k.Value.ToString();

            // 個別設定があるか確認
            if (config.Inventory.TryGetValue(activeCurrency, out var inventorySettings) &&
                inventorySettings.Denominations.TryGetValue(keyStr, out var setting))
            {
                return new CashStatusMonitor(
                    inventory,
                    k,
                    setting.NearEmpty,
                    setting.NearFull,
                    setting.Full);
            }

            // 個別設定がない場合はグローバルなデフォルトを使用
            return new CashStatusMonitor(
                inventory,
                k,
                config.Thresholds.NearEmpty,
                config.Thresholds.NearFull,
                config.Thresholds.Full);
        }).ToList();
    }

    /// <summary>設定オブジェクトを元に、全モニターのしきい値を更新する（ホットリロード用）。</summary>
    public void UpdateThresholdsFromConfig(SimulatorConfiguration config)
    {
        var activeCurrency = config.System.CurrencyCode ?? "JPY";
        foreach (var monitor in Monitors)
        {
            var k = monitor.Key;
            var keyStr = (k.Type == MoneyKind4Opos.Currencies.Interfaces.CashType.Bill ? "B" : "C") + k.Value.ToString();

            if (config.Inventory.TryGetValue(activeCurrency, out var inventorySettings) &&
                inventorySettings.Denominations.TryGetValue(keyStr, out var setting))
            {
                monitor.UpdateThresholds(
                    setting.NearEmpty,
                    setting.NearFull,
                    setting.Full);
            }
            else
            {
                monitor.UpdateThresholds(
                    config.Thresholds.NearEmpty,
                    config.Thresholds.NearFull,
                    config.Thresholds.Full);
            }
        }
    }
}
