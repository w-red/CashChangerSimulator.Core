namespace CashChangerSimulator.Core.Services.DeviceEventTypes;

/// <summary>
/// Event arguments for direct IO events.
/// ダイレクトIOイベント用のイベント引数。
/// </summary>
public class DeviceDirectIOEventArgs : DeviceEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceDirectIOEventArgs"/> class.
    /// </summary>
    /// <param name="eventNumber">The event number.</param>
    /// <param name="data">The data associated with the event.</param>
    /// <param name="obj">The object associated with the event.</param>
    public DeviceDirectIOEventArgs(int eventNumber, int data, object? obj)
    {
        EventNumber = eventNumber;
        Data = data;
        EventObject = obj;
    }

    /// <summary>
    /// Gets the event number.
    /// イベント番号を取得します。
    /// </summary>
    public int EventNumber { get; }

    /// <summary>
    /// Gets the data associated with the event.
    /// イベントに関連付けられたデータを取得します。
    /// </summary>
    public int Data { get; }

    /// <summary>
    /// Gets the object associated with the event.
    /// イベントに関連付けられたオブジェクトを取得します。
    /// </summary>
    public object? EventObject { get; }
}
