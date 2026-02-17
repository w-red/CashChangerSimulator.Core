using CsToml;

namespace CashChangerSimulator.Core.Configuration;

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
    public Dictionary<string, DenominationSettings> Denominations { get; set; } = [];
}
