namespace CashChangerSimulator.Core.Models;

/// <summary>デバイスの論理状態を表す列挙型。</summary>
public enum DeviceControlState
{
    /// <summary>なし。</summary>
    None = 0,

    /// <summary>クローズ状態。</summary>
    Closed = 1,

    /// <summary>待機状態(利用可能)。</summary>
    Idle = 2,

    /// <summary>実行中状態。</summary>
    Busy = 3,

    /// <summary>エラー(リカバリ待ち)。</summary>
    Error = 4,
}
