using Microsoft.PointOfService;

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
    public void QueueEvent(EventArgs args)
    {
        if (args is StatusUpdateEventArgs statusArgs)
        {
            // [FIX] Specific queueing for StatusUpdateEventArgs (required by coordinator/notifier tests)
            // [修正] コーディネーター/通知テストで必要な StatusUpdateEventArgs 用の特定のキューイング
            _sink?.QueueStatusUpdateEvent(statusArgs);
        }
        else if (args is DataEventArgs dataArgs)
        {
            // [FIX] Specific queueing for DataEventArgs
            // [修正] DataEventArgs 用の特定のキューイング
            _sink?.QueueDataEvent(dataArgs);
        }
        else
        {
            _sink?.QueueEvent(args);
        }
    }

    /// <inheritdoc/>
    public void SetAsyncProcessing(bool isBusy)
    {
        // Mediator 側で管理されることが多いため、ここでは通知のみを行うか、何もしません。
    }

    /// <inheritdoc/>
    public ControlState State => _sink?.State ?? ControlState.Closed;

    /// <inheritdoc/>
    public bool DeviceEnabled { get => _sink?.DeviceEnabled ?? false; set { if (_sink != null) _sink.DeviceEnabled = value; } }

    /// <inheritdoc/>
    public bool Claimed { get => _sink?.Claimed ?? false; set { if (_sink != null) _sink.Claimed = value; } }

    /// <inheritdoc/>
    public bool ClaimedByAnother { get => _sink?.ClaimedByAnother ?? false; set { if (_sink != null) _sink.ClaimedByAnother = value; } }

    /// <inheritdoc/>
    public bool DataEventEnabled => _sink?.DataEventEnabled ?? false;

    /// <inheritdoc/>
    public bool RealTimeDataEnabled => _sink?.RealTimeDataEnabled ?? false;

    /// <inheritdoc/>
    public bool DisableUposEventQueuing => _sink?.DisableUposEventQueuing ?? false;

    /// <inheritdoc/>
    public int AsyncResultCode
    {
        get => _sink?.AsyncResultCode ?? 0;
        set { if (_sink != null) _sink.AsyncResultCode = value; }
    }

    /// <inheritdoc/>
    public int AsyncResultCodeExtended
    {
        get => _sink?.AsyncResultCodeExtended ?? 0;
        set { if (_sink != null) _sink.AsyncResultCodeExtended = value; }
    }
}
