namespace CashChangerSimulator.Core.Services.DeviceEventTypes;

/// <summary>ステータス更新イベント用のイベント引数。</summary>
public class DeviceStatusUpdateEventArgs : DeviceEventArgs
{
    /// <summary>ステータスを指定してインスタンスを初期化します。</summary>
    /// <param name="status">The status of the device.</param>
    public DeviceStatusUpdateEventArgs(int status)
    {
        Status = status;
    }

    /// <summary>デバイスのステータスを取得します。</summary>
    public int Status { get; }
}
