namespace CashChangerSimulator.Core.Services.DeviceEventTypes;

/// <summary>
/// Event arguments for output completion events.
/// 出力完了イベント用のイベント引数。
/// </summary>
public class DeviceOutputCompleteEventArgs : DeviceEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceOutputCompleteEventArgs"/> class.
    /// </summary>
    /// <param name="outputId">The ID of the output that completed.</param>
    public DeviceOutputCompleteEventArgs(int outputId)
    {
        OutputId = outputId;
    }

    /// <summary>
    /// Gets the ID of the output that completed.
    /// 完了した出力のIDを取得します。
    /// </summary>
    public int OutputId { get; }
}
