namespace CashChangerSimulator.Core.Models;

/// <summary>入金確定時のアクション。</summary>
public enum DepositAction
{
    /// <summary>なし。</summary>
    None = 0,

    /// <summary>無変化（金庫へ移動、釣銭計算なし）。</summary>
    NoChange = 1,

    /// <summary>返却。</summary>
    Repay = 2,

    /// <summary>差額支払（RequiredAmount を超える分を釣銭として払い出し、残りを収納）。</summary>
    Change = 3,
}
