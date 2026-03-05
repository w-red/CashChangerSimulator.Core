namespace CashChangerSimulator.Core.Models;

/// <summary>貨幣の種別（硬貨 または 紙幣）を表します。</summary>
public enum CurrencyCashType
{
    /// <summary>未定義</summary>
    Undefined = 0,

    /// <summary>硬貨</summary>
    Coin = 1,

    /// <summary>紙幣</summary>
    Bill = 2
}
