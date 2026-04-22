namespace CashChangerSimulator.Core.Services.DeviceEventTypes;

/// <summary>DirectIOイベント用のイベント引数。</summary>
/// <param name="eventNumber">イベント番号。</param>
/// <param name="data">数値データ。</param>
/// <param name="objectData">オブジェクトデータ。</param>
public class DeviceDirectIOEventArgs(
    int eventNumber,
    int data,
    object objectData) : DeviceEventArgs
{

    /// <summary>イベント番号。</summary>
    public int EventNumber { get; } = eventNumber;

    /// <summary>数値データ。</summary>
    public int Data { get; } = data;

    /// <summary>オブジェクトデータ。</summary>
    public object ObjectData { get; } = objectData;
}
