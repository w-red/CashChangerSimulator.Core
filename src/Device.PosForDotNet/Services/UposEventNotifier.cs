using CashChangerSimulator.Device.Virtual;
using CashChangerSimulator.Device.PosForDotNet;

namespace CashChangerSimulator.Device.PosForDotNet.Services;

/// <summary>UPOS デバイスからのイベント通知を管理・抽象化するクラス。</summary>
public class UposEventNotifier : IUposEventNotifier
{
    private IUposEventSink? _sink;

    /// <summary>イベント通知先を指定せずに初期化します（後で Initialize を呼ぶ必要があります）。</summary>
    public UposEventNotifier()
    {
    }

    /// <summary>イベント通知先を指定して初期化します。</summary>
    public UposEventNotifier(IUposEventSink sink)
    {
        _sink = sink;
    }

    /// <inheritdoc/>
    public void Initialize(IUposEventSink sink)
    {
        _sink = sink;
    }

    /// <inheritdoc/>
    public void NotifyEvent(EventArgs e)
    {
        _sink?.NotifyEvent(e);
    }

    /// <inheritdoc/>
    public void FireEvent(EventArgs e)
    {
        _sink?.NotifyEvent(e);
    }

    /// <inheritdoc/>
    public void QueueEvent(EventArgs e)
    {
        _sink?.QueueEvent(e);
    }

    /// <inheritdoc/>
    public void SetAsyncProcessing(bool isBusy)
    {
        // Mediator 側で管理されることが多いため、ここでは通知のみを行うか、何もしません。
    }

    /// <inheritdoc/>
    public bool DataEventEnabled => _sink?.DataEventEnabled ?? false;

    /// <inheritdoc/>
    public bool RealTimeDataEnabled => _sink?.RealTimeDataEnabled ?? false;
}
