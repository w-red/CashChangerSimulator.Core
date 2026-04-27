namespace CashChangerSimulator.Core.Models;

/// <summary>デバイス操作の共通エラーコード。</summary>
public enum DeviceErrorCode
{
    /// <summary>正常終了。</summary>
    Success = 0,

    /// <summary>デバイスが閉じている。(OPOS_E_CLOSED).</summary>
    Closed = 101,

    /// <summary>デバイスが他から占有されている。(OPOS_E_CLAIMED).</summary>
    Claimed = 102,

    /// <summary>デバイスがオープンされていない。(OPOS_E_NOTOPEN).</summary>
    NotOpen = 103,

    /// <summary>デバイスが無効状態。(OPOS_E_DISABLED).</summary>
    Disabled = 104,

    /// <summary>デバイスがオープンされているが、占有されていない。(OPOS_E_NOTCLAIMED).</summary>
    NotClaimed = 105,

    /// <summary>不正なパラメータまたは状態での呼び出し。(OPOS_E_ILLEGAL).</summary>
    Illegal = 106,

    /// <summary>物理的なハードウェアが見つからない。(OPOS_E_NOHARDWARE).</summary>
    NoHardware = 107,

    /// <summary>デバイスがオフライン。(OPOS_E_OFFLINE).</summary>
    Offline = 108,

    /// <summary>サービスが利用できない。(OPOS_E_NOSERVICE).</summary>
    NoService = 109,

    /// <summary>致命的な失敗。(OPOS_E_FAILURE).</summary>
    Failure = 111,

    /// <summary>応答タイムアウト。(OPOS_E_TIMEOUT).</summary>
    Timeout = 112,

    /// <summary>デバイスがビジー状態。(OPOS_E_BUSY).</summary>
    Busy = 113,

    /// <summary>拡張エラーが発生。(OPOS_E_EXTENDED).</summary>
    Extended = 114,

    /// <summary>在庫不足(シミュレータ固有)。</summary>
    NoInventory = 118,

    /// <summary>未実装の機能。</summary>
    Unimplemented = 119,

    /// <summary>ビジー且つサービス停止状態。</summary>
    BusyWithNoService = 122,

    /// <summary>ハードウェア・ジャムが発生。</summary>
    Jammed = 300,

    /// <summary>処理が重複している。</summary>
    Overlapped = 301,

    /// <summary>処理がキャンセル(クリア)された。</summary>
    Cancelled = 115,
}
