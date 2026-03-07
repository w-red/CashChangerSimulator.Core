using Microsoft.PointOfService;
using Microsoft.Extensions.Logging;
using CashChangerSimulator.Core;

namespace CashChangerSimulator.Device.Services;

/// <summary>UPOS デバイスからのイベント通知を管理するクラス。</summary>
public class UposEventNotifier : IUposEventNotifier
{
    private readonly IUposEventSink _sink;
    private readonly ILogger<UposEventNotifier> _logger = LogProvider.CreateLogger<UposEventNotifier>();

    public UposEventNotifier(IUposEventSink sink)
    {
        _sink = sink;
    }

    public void NotifyEvent(System.EventArgs e)
    {
        // DataEvent の制約を確認
        if (e is DataEventArgs)
        {
            if (!_sink.CapDepositDataEvent || !_sink.DataEventEnabled)
            {
                return;
            }
        }

        if (_sink.SkipStateVerification) return;
        _sink.QueueEvent(e);
    }

    public void FireEvent(System.EventArgs e) => NotifyEvent(e);
}
