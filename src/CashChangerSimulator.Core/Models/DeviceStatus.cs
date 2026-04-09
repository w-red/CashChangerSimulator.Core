namespace CashChangerSimulator.Core.Models;

/// <summary>デバイスの状態更新イベントのステータスコード。</summary>
public enum DeviceStatus
{
    /// <summary>なし、または不明。</summary>
    None = 0,

    /// <summary>電源 ON。</summary>
    PowerOn = 2001,

    /// <summary>電源 OFF または切断。</summary>
    PowerOff = 2002,

    /// <summary>正常状態。</summary>
    JournalOk = 12,

    /// <summary>ジャム、またはエラー状態。</summary>
    JournalEmpty = 11,
}
