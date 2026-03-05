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
    public MonitorsProvider(Inventory inventory, ConfigurationProvider configProvider, ICurrencyMetadataProvider metadataProvider)
    {
        var config = configProvider.Config;
        var keys = metadataProvider.SupportedDenominations;

        Monitors = keys.Select(k =>
        {
            var activeCurrency = config.System.CurrencyCode ?? "JPY";
            // 金種キーを文字列に変換 (B1000, C100 等)
            var keyStr = (k.Type == CurrencyCashType.Bill ? "B" : "C") + k.Value.ToString();

            // 個別設定があるか確認
            if (config.Inventory.TryGetValue(activeCurrency, out var inventorySettings) &&
                inventorySettings.Denominations.TryGetValue(keyStr, out var setting))
            {
                return new CashStatusMonitor(
                    inventory,
                    k,
                    setting.IsRecyclable ? setting.NearEmpty : -1,
                    setting.IsRecyclable ? setting.NearFull : -1,
                    setting.IsRecyclable ? setting.Full : -1);
            }

            // 個別設定がない場合は全体のデフォルト（およびグローバルの IsRecyclable 指定）を使用
            var globalSetting = config.GetDenominationSetting(k);
            return new CashStatusMonitor(
                inventory,
                k,
                globalSetting.IsRecyclable ? config.Thresholds.NearEmpty : -1,
                globalSetting.IsRecyclable ? config.Thresholds.NearFull : -1,
                globalSetting.IsRecyclable ? config.Thresholds.Full : -1);
        }).ToList();
    }

    /// <summary>設定オブジェクトを元に、全モニターのしきい値を更新する（ホットリロード用）。</summary>
    public void UpdateThresholdsFromConfig(SimulatorConfiguration config)
    {
        var activeCurrency = config.System.CurrencyCode ?? "JPY";
        foreach (var monitor in Monitors)
        {
            var k = monitor.Key;
            var keyStr = (k.Type == CurrencyCashType.Bill ? "B" : "C") + k.Value.ToString();

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
}
