using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.Services;

/// <summary>UPOS デバイスからのイベント通知を管理するクラス。</summary>
public class UposEventNotifier : IUposEventNotifier
{
    private readonly IUposEventSink _sink;

    public UposEventNotifier(IUposEventSink sink)
    {
        _sink = sink;
    }

    public void NotifyEvent(System.EventArgs e)
    {
        if (_sink.SkipStateVerification) return;

        // DataEvent の制約を確認
        if (e is DataEventArgs)
        {
            if (!_sink.CapDepositDataEvent || !_sink.DataEventEnabled)
            {
                return;
            }
        }

        _sink.QueueEvent(e);
    }

    public void FireEvent(System.EventArgs e) => NotifyEvent(e);
}
