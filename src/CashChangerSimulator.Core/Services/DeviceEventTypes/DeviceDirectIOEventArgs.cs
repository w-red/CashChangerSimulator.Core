namespace CashChangerSimulator.Core.Services.DeviceEventTypes;

/// <summary>ダイレクトIOイベント用のイベント引数。</summary>
public class DeviceDirectIOEventArgs : DeviceEventArgs
{
    /// <summary>イベント情報を指定してインスタンスを初期化します。</summary>
    /// <param name="eventNumber">The event number.</param>
    /// <param name="data">The data associated with the event.</param>
    /// <param name="obj">The object associated with the event.</param>
    public DeviceDirectIOEventArgs(int eventNumber, int data, object? obj)
    {
        EventNumber = eventNumber;
        Data = data;
        EventObject = obj;
    }

    /// <summary>イベント番号を取得します。</summary>
    public int EventNumber { get; }

    /// <summary>イベントに関連付けられたデータを取得します。</summary>
    public int Data { get; }

    /// <summary>イベントに関連付けられたオブジェクトを取得します。</summary>
    public object? EventObject { get; }
}
