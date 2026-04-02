namespace CashChangerSimulator.Device;

/// <summary>デバイス操作の共通エラーコード。</summary>
public enum DeviceErrorCode
{
    /// <summary>正常終了。</summary>
    Success = 0,
    /// <summary>デバイスが閉じている。 (OPOS_E_CLOSED)</summary>
    Closed = 101,
    /// <summary>デバイスが他から占有されている。 (OPOS_E_CLAIMED)</summary>
    Claimed = 102,
    /// <summary>デバイスがオープンされていない。 (OPOS_E_NOTOPEN)</summary>
    NotOpen = 103,
    /// <summary>デバイスが無効状態。 (OPOS_E_DISABLED)</summary>
    Disabled = 104,
    /// <summary>デバイスがオープンされているが、占有されていない。 (OPOS_E_NOTCLAIMED)</summary>
    NotClaimed = 105,
    /// <summary>不正なパラメータまたは状態での呼び出し。 (OPOS_E_ILLEGAL)</summary>
    Illegal = 106,
    /// <summary>物理的なハードウェアが見つからない。 (OPOS_E_NOHARDWARE)</summary>
    NoHardware = 107,
    /// <summary>デバイスがオフライン。 (OPOS_E_OFFLINE)</summary>
    Offline = 108,
    /// <summary>サービスが利用できない。 (OPOS_E_NOSERVICE)</summary>
    NoService = 109,
    /// <summary>致命的な失敗。 (OPOS_E_FAILURE)</summary>
    Failure = 111,
    /// <summary>応答タイムアウト。 (OPOS_E_TIMEOUT)</summary>
    Timeout = 112,
    /// <summary>デバイスがビジー状態。 (OPOS_E_BUSY)</summary>
    Busy = 113,
    /// <summary>拡張エラーが発生。 (OPOS_E_EXTENDED)</summary>
    Extended = 114,
    /// <summary>在庫不足（シミュレータ固有）。</summary>
    NoInventory = 118,
    /// <summary>未実装の機能。</summary>
    Unimplemented = 119,
    /// <summary>ビジー且つサービス停止状態。</summary>
    BusyWithNoService = 122,
    /// <summary>ハードウェア・ジャムが発生。</summary>
    Jammed = 300,
    /// <summary>処理が重複している。</summary>
    Overlapped = 301
}

/// <summary>入金状態を表す列挙型。</summary>
public enum DeviceDepositStatus
{
    /// <summary>未開始。</summary>
    None = 0,
    /// <summary>受付開始。</summary>
    Start = 1,
    /// <summary>計数中。</summary>
    Counting = 2,
    /// <summary>受付終了・確定待ち。</summary>
    Validation = 3,
    /// <summary>入金完了。</summary>
    End = 4
}

/// <summary>出金・払い出し状態を表す列挙型。</summary>
public enum DeviceDispenseStatus
{
    /// <summary>正常終了。</summary>
    OK = 1,
    /// <summary>在庫なし（空）。</summary>
    Empty = 2,
    /// <summary>在庫僅少。</summary>
    NearEmpty = 3,
    /// <summary>満杯。</summary>
    Full = 4,
    /// <summary>満杯間近。</summary>
    NearFull = 5,
    /// <summary>ジャム発生。</summary>
    Jammed = 6,
    /// <summary>払い出し失敗。</summary>
    Failure = 10,
    /// <summary>一部払い出し成功。</summary>
    Partial = 11
}

/// <summary>デバイスの論理状態を表す列挙型。</summary>
public enum DeviceControlState
{
    /// <summary>クローズ状態。</summary>
    Closed = 1,
    /// <summary>待機状態（利用可能）。</summary>
    Idle = 2,
    /// <summary>実行中状態。</summary>
    Busy = 3,
    /// <summary>エラー（リカバリ待ち）。</summary>
    Error = 4
}

/// <summary>診断レベルを表す列挙型。</summary>
public enum DeviceHealthCheckLevel
{
    /// <summary>内部診断。</summary>
    Internal = 1,
    /// <summary>外部診断（ハードウェア連携含む）。</summary>
    External = 2,
    /// <summary>対話型診断。</summary>
    Interactive = 3
}

/// <summary>入金一時停止制御。</summary>
public enum DeviceDepositPause
{
    /// <summary>一時停止。</summary>
    Pause = 1,
    /// <summary>再開。</summary>
    Resume = 2
}

/// <summary>入金確定時のアクション。</summary>
public enum DepositAction
{
    /// <summary>収納（金庫へ移動）。</summary>
    Store = 1,
    /// <summary>返却。</summary>
    Repay = 2
}

/// <summary>特定の金種とその数量を保持します。</summary>
public record CashDenominationCount(decimal Denomination, int Count);
