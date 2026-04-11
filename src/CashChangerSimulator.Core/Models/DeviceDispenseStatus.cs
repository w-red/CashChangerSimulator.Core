namespace CashChangerSimulator.Core.Models;

/// <summary>出金・払い出し状態を表す列挙型。</summary>
public enum DeviceDispenseStatus
{
    /// <summary>なし。</summary>
    None = 0,

    /// <summary>正常終了。</summary>
    OK = 1,

    /// <summary>在庫なし(空)。</summary>
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
    Partial = 11,
}
