namespace CashChangerSimulator.Device;

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
    End = 4,
}
