namespace CashChangerSimulator.Core.Configuration;

/// <summary>在庫の初期設定を管理するクラス。</summary>
public class InventorySettings
{
    /// <summary>金種識別子（B=紙幣, C=硬貨 + 額面）と個別設定のマップ。</summary>
    public Dictionary<string, DenominationSettings> Denominations { get; set; } = [];
}
