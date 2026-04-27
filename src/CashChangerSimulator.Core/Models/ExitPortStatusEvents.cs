namespace CashChangerSimulator.Core.Models;

/// <summary>出金口の状態に関するベンダー拡張イベントコードの定義。</summary>
public static class ExitPortStatusEvents
{
    /// <summary>紙幣残あり (通常口)。</summary>
    public const int StatusBillRemainingNormal = 1000;

    /// <summary>紙幣クリア (通常口)。</summary>
    public const int StatusBillClearedNormal = 1001;

    /// <summary>紙幣残あり (回収口)。</summary>
    public const int StatusBillRemainingCollection = 1002;

    /// <summary>紙幣クリア (回収口)。</summary>
    public const int StatusBillClearedCollection = 1003;

    /// <summary>硬貨残あり (通常口)。</summary>
    public const int StatusCoinRemainingNormal = 1010;

    /// <summary>硬貨クリア (通常口)。</summary>
    public const int StatusCoinClearedNormal = 1011;

    /// <summary>硬貨残あり (回収口)。</summary>
    public const int StatusCoinRemainingCollection = 1012;

    /// <summary>硬貨クリア (回収口)。</summary>
    public const int StatusCoinClearedCollection = 1013;
}
