using R3;

namespace CashChangerSimulator.Core.Models;

/// <summary>読み取り専用の在庫情報のインターフェース。</summary>
public interface IReadOnlyInventory
{
    /// <summary>在庫が変更されたときに通知されるイベントストリーム。</summary>
    Observable<DenominationKey> Changed { get; }

    /// <summary>全在庫の金種キーと枚数の列挙を取得する。</summary>
    IEnumerable<KeyValuePair<DenominationKey, int>> AllCounts { get; }

    /// <summary>回収庫の全金種と枚数の列挙を取得する。</summary>
    IEnumerable<KeyValuePair<DenominationKey, int>> CollectionCounts { get; }

    /// <summary>リジェクト庫の全金種と枚数の列挙を取得する。</summary>
    IEnumerable<KeyValuePair<DenominationKey, int>> RejectCounts { get; }

    /// <summary>入金トレイ(エスクロー)の全金種と枚数の列挙を取得する。</summary>
    IEnumerable<KeyValuePair<DenominationKey, int>> EscrowCounts { get; }

    /// <summary>在庫の不一致が発生しているかどうかを取得します。</summary>
    /// <remarks>物理在庫と論理在庫の差(回収庫やリジェクト庫の有無)を示します。</remarks>
    bool HasDiscrepancy { get; }

    /// <summary>指定された金種の現在の枚数を取得する。</summary>
    /// <param name="key">金種キー。</param>
    /// <returns>在庫枚数。</returns>
    int GetCount(DenominationKey key);

    /// <summary>指定された金種の全庫(還流・回収・リジェクト)の合計枚数を取得する。</summary>
    /// <param name="key">金種キー。</param>
    /// <returns>合計枚数。</returns>
    int GetTotalCount(DenominationKey key);

    /// <summary>現在の在庫の合計金額を計算する。</summary>
    /// <param name="currencyCode">通貨コード(任意)。指定しない場合は全通貨の合計を返します。</param>
    /// <returns>合計金額。</returns>
    decimal CalculateTotal(string? currencyCode = null);
}
