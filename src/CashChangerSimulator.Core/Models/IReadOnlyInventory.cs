using R3;

namespace CashChangerSimulator.Core.Models;

/// <summary>読み取り専用の在庫情報のインターフェース。</summary>
public interface IReadOnlyInventory
{
    /// <summary>指定された金種の現在の枚数を取得する。</summary>
    int GetCount(DenominationKey key);
    /// <summary>現在の在庫の合計金額を計算する。</summary>
    decimal CalculateTotal(string? currencyCode = null);
    /// <summary>在庫が変更されたときに通知されるイベントストリーム。</summary>
    Observable<DenominationKey> Changed { get; }
    /// <summary>全在庫の金種キーと枚数の列挙を取得する。</summary>
    System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<DenominationKey, int>> AllCounts { get; }
    /// <summary>回収庫の全金種と枚数の列挙を取得する。</summary>
    System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<DenominationKey, int>> CollectionCounts { get; }
    /// <summary>リジェクト庫の全金種と枚数の列挙を取得する。</summary>
    System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<DenominationKey, int>> RejectCounts { get; }
    /// <summary>在庫の不一致が発生しているかどうかを取得します。</summary>
    /// <remarks>物理在庫と論理在庫の差（回収庫やリジェクト庫の有無）を示します。</remarks>
    bool HasDiscrepancy { get; }
}
