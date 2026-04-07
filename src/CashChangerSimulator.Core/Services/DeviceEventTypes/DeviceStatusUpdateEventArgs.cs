namespace CashChangerSimulator.Core.Services.DeviceEventTypes;

/// <summary>
/// Event arguments for status update events.
/// ステータス更新イベント用のイベント引数。
/// </summary>
public class DeviceStatusUpdateEventArgs : DeviceEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceStatusUpdateEventArgs"/> class.
    /// </summary>
    /// <param name="status">The status of the device.</param>
    public DeviceStatusUpdateEventArgs(int status)
    {
        Status = status;
    }

    /// <summary>
    /// Gets the status of the device.
    /// デバイスのステータスを取得します。
    /// </summary>
    public int Status { get; }
}
