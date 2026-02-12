namespace CashChangerSimulator.Core.Models;

/// <summary>
/// 取引の種類を表す列挙型。
/// </summary>
public enum TransactionType
{
    /// <summary>不明な取引。</summary>
    Unknown,
    /// <summary>入金（顧客からの投入）。</summary>
    Deposit,
    /// <summary>出金（お釣りや支払い）。</summary>
    Dispense,
    /// <summary>手動補充（管理者による追加）。</summary>
    Refill,
    /// <summary>手動回収（管理者による取り出し）。</summary>
    Collection,
    /// <summary>在庫調整（棚卸し等による直接修正）。</summary>
    Adjustment
}
