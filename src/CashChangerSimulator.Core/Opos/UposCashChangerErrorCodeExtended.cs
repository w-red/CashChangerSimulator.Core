namespace CashChangerSimulator.Core.Opos;

/// <summary>UnifiedPOS CashChanger の ResultCodeExtended 定数。</summary>
public enum UposCashChangerErrorCodeExtended
{
    /// <summary>現金不足のため、指定された現金を払い出せない。</summary>
    OverDispense = 201,

    /// <summary>払い出し数が不足している。</summary>
    UnderDispense = 202,

    /// <summary>在高の不一致が検出された。</summary>
    Discrepancy = 203,

    /// <summary>ジャムが発生した。</summary>
    Jam = 204,

    /// <summary>現金が空になった。</summary>
    Empty = 205,

    /// <summary>現金がいっぱいになった。</summary>
    Full = 206
}
