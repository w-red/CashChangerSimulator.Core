namespace CashChangerSimulator.Core.Models;

/// <summary>釣銭機の状態を表す列挙型。</summary>
public enum CashStatus
{
    /// <summary>状態が判定できない、または不明。</summary>
    Unknown,
    /// <summary>在庫が空。</summary>
    Empty,
    /// <summary>在庫が空に近い（補充が必要）。</summary>
    NearEmpty,
    /// <summary>正常な在庫量。</summary>
    Normal,
    /// <summary>在庫が満杯に近い（回収が必要）。</summary>
    NearFull,
    /// <summary>在庫が満杯。</summary>
    Full
}
