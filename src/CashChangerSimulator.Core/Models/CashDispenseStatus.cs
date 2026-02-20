namespace CashChangerSimulator.Core.Models;

/// <summary>出金操作のステータスを定義する列挙型。</summary>
public enum CashDispenseStatus
{
    /// <summary>待機中。</summary>
    Idle,
    /// <summary>出金処理中。</summary>
    Busy,
    /// <summary>エラー発生中。</summary>
    Error
}
