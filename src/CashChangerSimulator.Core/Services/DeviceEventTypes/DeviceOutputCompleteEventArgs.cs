namespace CashChangerSimulator.Core.Services.DeviceEventTypes;

/// <summary>出力完了イベント用のイベント引数。</summary>
public class DeviceOutputCompleteEventArgs : DeviceEventArgs
{
    /// <summary>出力 ID を指定してインスタンスを初期化します。</summary>
    /// <param name="outputId">The ID of the output that completed.</param>
    public DeviceOutputCompleteEventArgs(int outputId)
    {
        OutputId = outputId;
    }

    /// <summary>完了した出力の ID を取得します。</summary>
    public int OutputId { get; }
}
