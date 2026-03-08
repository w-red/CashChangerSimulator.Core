using CashChangerSimulator.Core.Models;

namespace CashChangerSimulator.Core.Configuration;

/// <summary>釣銭機シミュレーターの全設定を統括するルートモデル。</summary>
/// <remarks>
/// TOML 設定ファイルからデシリアライズされる全設定（システム、在庫、金種、しきい値、ログ等）を保持します。
/// デバイスの動作条件やシミュレーションの振る舞いを決定づける設定値へのアクセスを提供します。
/// </remarks>
public class SimulatorConfiguration
{
    /// <summary>全般的なシステム設定。</summary>
    public SystemSettings System { get; set; } = new();

    /// <summary>通貨コードごとの在庫設定。</summary>
    public Dictionary<string, InventorySettings> Inventory { get; set; } = new()
    {
        ["JPY"] = new InventorySettings
        {
            Denominations = new()
            {
                ["B10000"] = new() { InitialCount = 10, DisplayName = "10,000 Yen Bill", DisplayNameJP = "一万円札" },
                ["B5000"] = new() { InitialCount = 10, DisplayName = "5,000 Yen Bill", DisplayNameJP = "五千円札" },
                ["B2000"] = new() { InitialCount = 0, NearEmpty = -1, IsRecyclable = false, IsDepositable = true, DisplayName = "2,000 Yen Bill", DisplayNameJP = "二千円札" },
                ["B1000"] = new() { InitialCount = 50, DisplayName = "1,000 Yen Bill", DisplayNameJP = "千円札" },
                ["C500"] = new() { InitialCount = 50, DisplayName = "500 Yen Coin", DisplayNameJP = "五百円玉" },
                ["C100"] = new() { InitialCount = 100, DisplayName = "100 Yen Coin", DisplayNameJP = "百円玉" },
                ["C50"] = new() { InitialCount = 100, DisplayName = "50 Yen Coin", DisplayNameJP = "五十円玉" },
                ["C10"] = new() { InitialCount = 100, DisplayName = "10 Yen Coin", DisplayNameJP = "十円玉" },
                ["C5"] = new() { InitialCount = 100, DisplayName = "5 Yen Coin", DisplayNameJP = "五円玉" },
                ["C1"] = new() { InitialCount = 100, DisplayName = "1 Yen Coin", DisplayNameJP = "一円玉" },
            }
        },
        ["USD"] = new InventorySettings
        {
            Denominations = new()
            {
                ["B100"] = new() { InitialCount = 5, DisplayName = "$100 Bill" },
                ["B50"] = new() { InitialCount = 5, DisplayName = "$50 Bill" },
                ["B20"] = new() { InitialCount = 10, DisplayName = "$20 Bill" },
                ["B10"] = new() { InitialCount = 10, DisplayName = "$10 Bill" },
                ["B5"] = new() { InitialCount = 20, DisplayName = "$5 Bill" },
                ["B1"] = new() { InitialCount = 50, DisplayName = "$1 Bill" },
                ["C1"] = new() { InitialCount = 50, DisplayName = "$1 Coin" },
                ["C0.5"] = new() { InitialCount = 50, DisplayName = "50¢ Coin" },
                ["C0.25"] = new() { InitialCount = 100, DisplayName = "25¢ Coin" },
                ["C0.1"] = new() { InitialCount = 100, DisplayName = "10¢ Coin" },
                ["C0.05"] = new() { InitialCount = 100, DisplayName = "5¢ Coin" },
                ["C0.01"] = new() { InitialCount = 100, DisplayName = "1¢ Coin" },
            }
        }
    };

    /// <summary>デフォルトのしきい値設定。</summary>
    public ThresholdSettings Thresholds { get; set; } = new();

    /// <summary>ロギング設定。</summary>
    public LoggingSettings Logging { get; set; } = new();

    /// <summary>シミュレーション設定。</summary>
    public SimulationSettings Simulation { get; set; } = new();

    /// <summary>指定された金種の個別設定を取得する。存在しない場合はデフォルト値を返す。</summary>
    public DenominationSettings GetDenominationSetting(DenominationKey key)
    {
        var keyStr = key.ToDenominationString();
        return Inventory.TryGetValue(key.CurrencyCode, out var inventory) &&
            inventory.Denominations.TryGetValue(keyStr, out var setting)
            ? setting
            : new DenominationSettings
            {
                NearEmpty = Thresholds.NearEmpty,
                NearFull = Thresholds.NearFull,
                Full = Thresholds.Full
            };
    }
}
