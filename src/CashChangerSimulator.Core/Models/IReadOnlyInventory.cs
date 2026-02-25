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
}
