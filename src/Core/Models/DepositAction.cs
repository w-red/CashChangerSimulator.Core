namespace CashChangerSimulator.Device;

/// <summary>入金確定時のアクション。.</summary>
public enum DepositAction
{
    /// <summary>なし。.</summary>
    None = 0,

    /// <summary>収納（金庫へ移動）。.</summary>
    Store = 1,

    /// <summary>返却。.</summary>
    Repay = 2,
}
