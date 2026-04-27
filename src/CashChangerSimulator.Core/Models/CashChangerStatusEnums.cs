namespace CashChangerSimulator.Core.Models;

/// <summary>釣銭機の在庫不足状態を表す列挙型。</summary>
public enum CashChangerStatus
{
    /// <summary>正常。</summary>
    OK = 0,

    /// <summary>空の状態。</summary>
    Empty = 11,

    /// <summary>空に近い状態。</summary>
    NearEmpty = 12,
}

/// <summary>釣銭機の満杯状態を表す列挙型。</summary>
public enum CashChangerFullStatus
{
    /// <summary>正常。</summary>
    OK = 0,

    /// <summary>満杯の状態。</summary>
    Full = 21,

    /// <summary>満杯に近い状態。</summary>
    NearFull = 22,
}
