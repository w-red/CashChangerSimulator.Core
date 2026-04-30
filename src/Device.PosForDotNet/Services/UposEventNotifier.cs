using Microsoft.PointOfService;
using CashChangerSimulator.Core.Services;

namespace CashChangerSimulator.Device.PosForDotNet.Services;

/// <summary>UPOS デバイスからのイベント通知を管理・抽象化するクラス。</summary>
public class UposEventNotifier : IUposEventNotifier
{
    private IUposEventSink? sink;

    /// <summary>Initializes a new instance of the <see cref="UposEventNotifier"/> class.イベント通知先を指定せずに初期化します(後で Initialize を呼ぶ必要があります)。</summary>
    public UposEventNotifier()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="UposEventNotifier"/> class.イベント通知先を指定して初期化します。</summary>
    public UposEventNotifier(IUposEventSink sink)
    {
        this.sink = sink;
    }

    /// <inheritdoc/>
    public void Initialize(IUposEventSink sink)
    {
        this.sink = sink;
    }

    /// <inheritdoc/>
    public void NotifyEvent(EventArgs e)
    {
        sink?.NotifyEvent(e);
    }

    /// <inheritdoc/>
    public void FireEvent(EventArgs e)
    {
        sink?.NotifyEvent(e);
    }

    /// <inheritdoc/>
    public void QueueEvent(EventArgs e)
    {
        if (e is StatusUpdateEventArgs statusArgs)
        {
            // [FIX] Specific queueing for StatusUpdateEventArgs (required by coordinator/notifier tests)
            sink?.QueueStatusUpdateEvent(statusArgs);
        }
        else if (e is DataEventArgs dataArgs)
        {
            // [FIX] Specific queueing for DataEventArgs with filtering
            // [修正] フィルタリング付きの DataEventArgs 用の特定のキューイング
            if (DataEventEnabled && CapDepositDataEvent)
            {
                sink?.QueueDataEvent(dataArgs);
            }
        }
        else
        {
            sink?.QueueEvent(e);
        }
    }

    /// <inheritdoc/>
    public bool CapDepositDataEvent =>
        sink?.CapDepositDataEvent ?? false;

    /// <inheritdoc/>
    public void SetAsyncProcessing(bool isBusy)
    {
        // Mediator 側で管理されることが多いため、ここでは通知のみを行うか、何もしません。
    }

    /// <inheritdoc/>
    public ControlState State =>
        sink?.State ?? ControlState.Closed;

    /// <inheritdoc/>
    public bool DeviceEnabled
    {
        get => sink?.DeviceEnabled ?? false;
        set => sink?.Apply(s => s.DeviceEnabled = value);
    }

    /// <inheritdoc/>
    public bool Claimed
    {
        get => sink?.Claimed ?? false;
        set => sink?.Apply(s => s.Claimed = value);
    }

    /// <inheritdoc/>
    public bool ClaimedByAnother
    {
        get => sink?.ClaimedByAnother ?? false;
        set => sink?.Apply(s => s.ClaimedByAnother = value);
    }

    /// <inheritdoc/>
    public bool DataEventEnabled => sink?.DataEventEnabled ?? false;

    /// <inheritdoc/>
    public bool RealTimeDataEnabled => sink?.RealTimeDataEnabled ?? false;

    /// <inheritdoc/>
    public bool DisableUposEventQueuing => sink?.DisableUposEventQueuing ?? false;

    /// <inheritdoc/>
    public int AsyncResultCode
    {
        get => sink?.AsyncResultCode ?? 0;
        set => sink?.Apply(s => s.AsyncResultCode = value);
    }

    /// <inheritdoc/>
    public int AsyncResultCodeExtended
    {
        get => sink?.AsyncResultCodeExtended ?? 0;
        set => sink?.Apply(s => s.AsyncResultCodeExtended = value);
    }
}
