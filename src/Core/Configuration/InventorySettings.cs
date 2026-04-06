namespace CashChangerSimulator.Core.Configuration;

/// <summary>在庫の初期設定を管理するクラス。.</summary>
public class InventorySettings
{
    /// <summary>Gets or sets 金種識別子（B=紙幣, C=硬貨 + 額面）と個別設定のマップ。.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Needed for TOML deserialization")]
    public Dictionary<string, DenominationSettings> Denominations { get; set; } = [];
}
