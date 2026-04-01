using R3;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.Services;

/// <summary>イベント通知の送り先となる SO 本体が実装するインターフェース。</summary>
/// <remarks>
/// Microsoft POS for .NET (OPOS) の基本クラス（CashChangerBasic等）が持つイベント通知メソッドへの橋渡しを行います。
/// </remarks>
public interface IUposEventSink
{
    /// <summary>DataEvent が有効かどうか。</summary>
    bool DataEventEnabled { get; }

    /// <summary>入金データイベントをサポートしているか。</summary>
    bool CapDepositDataEvent { get; }

    /// <summary>状態検証をスキップするかどうか。</summary>
    bool SkipStateVerification { get; }

    /// <summary>リアルタイムデータ通知が有効かどうか。</summary>
    bool RealTimeDataEnabled { get; }

    /// <summary>イベントを通知し、必要に応じてキューに追加します。</summary>
    /// <param name="e">イベント引数。</param>
    void NotifyEvent(EventArgs e);

    /// <summary>イベントをキューに追加します。</summary>
    /// <param name="e">イベント引数。</param>
    void QueueEvent(EventArgs e);

    /// <summary>データイベントをキューに追加します。</summary>
    /// <param name="e">イベント引数。</param>
    void QueueDataEvent(DataEventArgs e);

    /// <summary>入金状態の変更を通知するストリームを取得します。</summary>
    Observable<Unit> DepositChanged { get; }

    /// <summary>ステータス更新イベントをキューに追加します。</summary>
    /// <param name="e">イベント引数。</param>
    void QueueStatusUpdateEvent(StatusUpdateEventArgs e);
}
