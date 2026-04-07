namespace CashChangerSimulator.Core.Services.DeviceEventTypes;

/// <summary>
/// Event arguments for data events.
/// データイベント用のイベント引数。
/// </summary>
public class DeviceDataEventArgs : DeviceEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceDataEventArgs"/> class.
    /// </summary>
    /// <param name="status">The status of the data.</param>
    public DeviceDataEventArgs(int status)
    {
        Status = status;
    }

    /// <summary>
    /// Gets the status of the data.
    /// データのステータスを取得します。
    /// </summary>
    public int Status { get; }
}
