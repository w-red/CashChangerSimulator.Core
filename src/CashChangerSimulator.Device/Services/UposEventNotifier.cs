using Microsoft.PointOfService;
using Microsoft.Extensions.Logging;
using CashChangerSimulator.Core;

namespace CashChangerSimulator.Device.Services;

/// <summary>UPOS デバイスからのイベント通知を管理する具象クラス。</summary>
/// <remarks>
/// `IUposEventSink` を通じて、実際のデバイステキスト（SimulatorCashChanger）へイベントを配信します。
/// データイベントの有効無効設定（DataEventEnabled）やバリデーションスキップ設定を考慮して通知を判断します。
/// </remarks>
public class UposEventNotifier(IUposEventSink sink) : IUposEventNotifier, ICashChangerStatusSink
{
    /// <summary>イベント通知先を指定して初期化します。</summary>
    private readonly IUposEventSink _sink = sink;
    private readonly ILogger<UposEventNotifier> _logger = LogProvider.CreateLogger<UposEventNotifier>();

    /// <summary>データイベントが有効かどうかを取得します。</summary>
    public bool DataEventEnabled => _sink.DataEventEnabled;

    /// <summary>リアルタイムデータ通知が有効かどうかを取得します。</summary>
    public bool RealTimeDataEnabled => _sink.RealTimeDataEnabled;

    /// <summary>イベントを通知し、必要に応じてキューに追加します。</summary>
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

    /// <summary>イベントを強制的に発生させます（NotifyEvent のエイリアス）。</summary>
    public void FireEvent(System.EventArgs e) => NotifyEvent(e);

    /// <summary>非同期処理中（ビジー状態）かどうかを設定します。</summary>
    public void SetAsyncProcessing(bool isBusy)
    {
        if (_sink is IDeviceStateProvider provider && provider is SimulatorCashChanger so)
        {
            so.SetAsyncProcessingInternal(isBusy);
        }
    }

    /// <summary>イベントを適切なキューに追加します。</summary>
    public void QueueEvent(System.EventArgs e)
    {
        if (e is DataEventArgs de)
        {
            _sink.QueueDataEvent(de);
        }
        else if (e is StatusUpdateEventArgs se)
        {
            _sink.QueueStatusUpdateEvent(se);
        }
    }
}
