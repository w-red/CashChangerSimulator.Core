namespace CashChangerSimulator.Core.Models;

/// <summary>出金口の種類を表す列挙型。</summary>
public enum ExitPort
{
    /// <summary>通常口 (釣銭払い出し用)。</summary>
    Normal = 0,

    /// <summary>回収口 (回収操作用)。</summary>
    Collection = 1,
}
