using System.Collections.Generic;
using CsToml;

namespace CashChangerSimulator.Core.Configuration;

/// <summary>
/// 釣銭機シミュレーターの設定を保持するクラス。
/// </summary>
[TomlSerializedObject]
public partial class SimulatorConfiguration
{
    /// <summary>通貨コード（例: "JPY", "USD"）。</summary>
    [TomlValueOnSerialized]
    public string CurrencyCode { get; set; } = "JPY";

    /// <summary>在庫の初期設定。</summary>
    [TomlValueOnSerialized]
    public InventorySettings Inventory { get; set; } = new();

    /// <summary>デフォルトのしきい値設定（金種別設定がない場合に使用）。</summary>
    [TomlValueOnSerialized]
    public ThresholdSettings Thresholds { get; set; } = new();
}

/// <summary>
/// 金種ごとの詳細設定を保持するクラス。
/// </summary>
[TomlSerializedObject]
public partial class DenominationSettings
{
    /// <summary>ユーザーが設定した表示名。</summary>
    [TomlValueOnSerialized]
    public string? DisplayName { get; set; }

    /// <summary>初期枚数。</summary>
    [TomlValueOnSerialized]
    public int InitialCount { get; set; }

    /// <summary>NearEmpty 判定値。</summary>
    [TomlValueOnSerialized]
    public int NearEmpty { get; set; } = 5;

    /// <summary>NearFull 判定値。</summary>
    [TomlValueOnSerialized]
    public int NearFull { get; set; } = 90;

    /// <summary>Full 判定値。</summary>
    [TomlValueOnSerialized]
    public int Full { get; set; } = 100;
}

/// <summary>
/// 在庫の初期設定を管理するクラス。
/// </summary>
[TomlSerializedObject]
public partial class InventorySettings
{
    /// <summary>
    /// 金種識別子（B=紙幣, C=硬貨 + 額面）と個別設定のマップ。
    /// </summary>
    [TomlValueOnSerialized]
    public Dictionary<string, DenominationSettings> Denominations { get; set; } = new();

    /// <summary>
    /// 互換性維持のための古い初期枚数設定。
    /// </summary>
    [TomlValueOnSerialized]
    [Obsolete("Use Denominations instead.")]
    public Dictionary<string, int> InitialCounts { get; set; } = new();
}

/// <summary>
/// ステータス判定用のしきい値を管理する設定クラス。
/// </summary>
[TomlSerializedObject]
public partial class ThresholdSettings
{
    /// <summary>NearEmpty と判定する枚数（この枚数以下の場合）。</summary>
    [TomlValueOnSerialized]
    public int NearEmpty { get; set; } = 5;
    
    /// <summary>NearFull と判定する枚数（この枚数以上の場合）。</summary>
    [TomlValueOnSerialized]
    public int NearFull { get; set; } = 90;
    
    /// <summary>Full と判定する枚数（この枚数以上の場合）。</summary>
    [TomlValueOnSerialized]
    public int Full { get; set; } = 100;
}
