namespace CashChangerSimulator.Core.Models;

/// <summary>入金一時停止制御。</summary>
public enum DeviceDepositPause
{
    /// <summary>なし。</summary>
    None = 0,

    /// <summary>一時停止。</summary>
    Pause = 1,

    /// <summary>再開。</summary>
    Resume = 2,
}
