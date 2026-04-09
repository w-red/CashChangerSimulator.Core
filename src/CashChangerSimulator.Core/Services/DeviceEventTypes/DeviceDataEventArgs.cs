namespace CashChangerSimulator.Core.Services.DeviceEventTypes;

/// <summary>データイベント用のイベント引数。</summary>
public class DeviceDataEventArgs : DeviceEventArgs
{
    /// <summary>ステータスを指定してインスタンスを初期化します。</summary>
    /// <param name="status">The status of the data.</param>
    public DeviceDataEventArgs(int status)
    {
        Status = status;
    }

    /// <summary>データのステータスを取得します。</summary>
    public int Status { get; }
}
