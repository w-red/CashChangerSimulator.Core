using CashChangerSimulator.Core.Models;
using CsToml;

namespace CashChangerSimulator.Core.Configuration;

/// <summary>釣銭機シミュレーターの設定を保持するクラス。</summary>
[TomlSerializedObject]
public partial class SimulatorConfiguration
{
    /// <summary>現在の通貨コード。</summary>
    [TomlValueOnSerialized]
    public string CurrencyCode { get; set; } = "JPY";

    /// <summary>通貨コードごとの在庫設定。</summary>
    [TomlValueOnSerialized]
    public Dictionary<string, InventorySettings> Inventory { get; set; } = new()
    {
        ["JPY"] = new InventorySettings
        {
            Denominations = new()
            {
                ["B10000"] = new() { InitialCount = 10, DisplayName = "10000円" },
                ["B5000"] = new() { InitialCount = 10, DisplayName = "5000円" },
                ["B1000"] = new() { InitialCount = 50, DisplayName = "1000円" },
                ["C500"] = new() { InitialCount = 50, DisplayName = "500円" },
                ["C100"] = new() { InitialCount = 100, DisplayName = "100円" },
                ["C50"] = new() { InitialCount = 100, DisplayName = "50円" },
                ["C10"] = new() { InitialCount = 100, DisplayName = "10円" },
                ["C5"] = new() { InitialCount = 100, DisplayName = "5円" },
                ["C1"] = new() { InitialCount = 100, DisplayName = "1円" },
            }
        },
        ["USD"] = new InventorySettings
        {
            Denominations = new()
            {
                ["B100"] = new() { InitialCount = 5, DisplayName = "$100" },
                ["B50"] = new() { InitialCount = 5, DisplayName = "$50" },
                ["B20"] = new() { InitialCount = 10, DisplayName = "$20" },
                ["B10"] = new() { InitialCount = 10, DisplayName = "$10" },
                ["B5"] = new() { InitialCount = 20, DisplayName = "$5" },
                ["B1"] = new() { InitialCount = 50, DisplayName = "$1" },
                ["C0.5"] = new() { InitialCount = 50, DisplayName = "50¢" },
                ["C0.25"] = new() { InitialCount = 100, DisplayName = "25¢" },
                ["C0.1"] = new() { InitialCount = 100, DisplayName = "10¢" },
                ["C0.05"] = new() { InitialCount = 100, DisplayName = "5¢" },
                ["C0.01"] = new() { InitialCount = 100, DisplayName = "1¢" },
            }
        }
    };

    /// <summary>デフォルトのしきい値設定（金種別設定がない場合に使用）。</summary>
    [TomlValueOnSerialized]
    public ThresholdSettings Thresholds { get; set; } = new();

    /// <summary>UIの動作モード。</summary>
    [TomlValueOnSerialized]
    public UIMode UIMode { get; set; } = UIMode.Standard;

    /// <summary>ロギング設定。</summary>
    [TomlValueOnSerialized]
    public LoggingSettings Logging { get; set; } = new();

    /// <summary>指定された金種の個別設定を取得する。存在しない場合はデフォルト値を返す。</summary>
    public DenominationSettings GetDenominationSetting(DenominationKey key)
    {
        var keyStr = (key.Type == MoneyKind4Opos.Currencies.Interfaces.CashType.Bill ? "B" : "C") + key.Value.ToString();
        if (Inventory.TryGetValue(key.CurrencyCode, out var inventory) &&
            inventory.Denominations.TryGetValue(keyStr, out var setting))
        {
            return setting;
        }

        return new DenominationSettings
        {
            NearEmpty = Thresholds.NearEmpty,
            NearFull = Thresholds.NearFull,
            Full = Thresholds.Full
        };
    }
}
