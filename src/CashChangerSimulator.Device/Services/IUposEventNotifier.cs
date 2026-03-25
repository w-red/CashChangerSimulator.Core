namespace CashChangerSimulator.Device.Services;

/// <summary>UPOS デバイスからのイベント通知を管理・抽象化するインターフェース。</summary>
/// <remarks>
/// DataEvent, StatusUpdateEvent などの UPOS 標準イベントのキューイングと通知を制御します。
/// シミュレータ内部の各種コンポーネントは、このインターフェースを通じてアプリケーション層へイベントを届けます。
/// </remarks>
public interface IUposEventNotifier : ICashChangerStatusSink
{
    /// <summary>イベントを通知キューに追加（または即時送信）します。</summary>
    /// <param name="e">イベント引数。</param>
    void NotifyEvent(EventArgs e);

    /// <summary>外部からイベントを強制的に発生させます。</summary>
    /// <param name="e">イベント引数。</param>
    new void FireEvent(EventArgs e);

    /// <summary>イベントを適切なキューに追加します。</summary>
    /// <param name="e">イベント引数。</param>
    void QueueEvent(EventArgs e);
}
