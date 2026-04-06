namespace CashChangerSimulator.Device;

/// <summary>診断レベルを表す列挙型。</summary>
public enum DeviceHealthCheckLevel
{
    /// <summary>なし。</summary>
    None = 0,

    /// <summary>内部診断。</summary>
    Internal = 1,

    /// <summary>外部診断（ハードウェア連携含む）。</summary>
    External = 2,

    /// <summary>対話型診断。</summary>
    Interactive = 3,
}
