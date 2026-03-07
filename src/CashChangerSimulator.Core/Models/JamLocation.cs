namespace CashChangerSimulator.Core.Models;

/// <summary>現金詰まり（ジャム）が発生した具体的な箇所を示す列挙型。</summary>
public enum JamLocation
{
    /// <summary>無し。</summary>
    None = 0,

    /// <summary>投入口。</summary>
    Inlet = 10,

    /// <summary>搬送路。</summary>
    Transport = 20,

    /// <summary>硬貨カセット 1。</summary>
    CoinCassette1 = 31,
    /// <summary>硬貨カセット 2。</summary>
    CoinCassette2 = 32,
    /// <summary>硬貨カセット 3。</summary>
    CoinCassette3 = 33,
    /// <summary>硬貨カセット 4。</summary>
    CoinCassette4 = 34,

    /// <summary>紙幣カセット 1。</summary>
    BillCassette1 = 41,
    /// <summary>紙幣カセット 2。</summary>
    BillCassette2 = 42,
    /// <summary>紙幣カセット 3。</summary>
    BillCassette3 = 43,
    /// <summary>紙幣カセット 4。</summary>
    BillCassette4 = 44,

    /// <summary>排出口。</summary>
    Outlet = 90
}
