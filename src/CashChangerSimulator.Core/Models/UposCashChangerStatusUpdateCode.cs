namespace CashChangerSimulator.Core.Models;

/// <summary>UnifiedPOS CashChanger の StatusUpdateEvent 定数。</summary>
public enum UposCashChangerStatusUpdateCode
{
    /// <summary>状態は正常。</summary>
    Ok = 0,
    /// <summary>いくつかの金種が空。</summary>
    Empty = 11,
    /// <summary>いくつかの金種が空に近い。</summary>
    NearEmpty = 12,
    /// <summary>金種が空の状態が解消された。</summary>
    EmptyOk = 13,
    /// <summary>いくつかの金種が満杯。</summary>
    Full = 21,
    /// <summary>いくつかの金種が満杯に近い。</summary>
    NearFull = 22,
    /// <summary>金種が満杯の状態が解消された。</summary>
    FullOk = 23,
    /// <summary>メカトラ（詰まり）が発生した。</summary>
    Jam = 31,
    /// <summary>デバイスが取り外された。</summary>
    Removed = 41,
    /// <summary>デバイスが装着された。</summary>
    Inserted = 42,

    /// <summary>非同期処理が完了した (CHAN_STATUS_ASYNC)。</summary>
    AsyncFinished = 91,

    /// <summary>ベンダー固有の JAM 状態 (現行実装で使用)。</summary>
    VendorJam = 205,
    /// <summary>ベンダー固有の OK 状態 (現行実装で使用)。</summary>
    VendorOk = 206
}
