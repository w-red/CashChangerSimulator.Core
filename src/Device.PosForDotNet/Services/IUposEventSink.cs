using Microsoft.PointOfService;
using R3;

namespace CashChangerSimulator.Device.PosForDotNet.Services;

/// <summary>イベント通知の送り先となる SO 本体が実装するインターフェース。</summary>
/// <remarks>
/// Microsoft POS for .NET (OPOS) の基本クラス(CashChangerBasic等)が持つイベント通知メソッドへの橋渡しを行います。
/// </remarks>
public interface IUposEventSink
{
    /// <summary>デバイスの状態(POS for .NET 標準)。</summary>
    ControlState State { get; }

    /// <summary>デバイスが占有されているかどうかを取得または設定します。</summary>
    bool Claimed { get; set; }

    /// <summary>他プロセスで占有されているかどうかを取得または設定します。</summary>
    bool ClaimedByAnother { get; set; }

    /// <summary>デバイスが有効化されているかどうかを取得または設定します。</summary>
    bool DeviceEnabled { get; set; }

    /// <summary>dataEvent が有効かどうかを取得します。</summary>
    bool DataEventEnabled { get; }

    /// <summary>入金データイベントをサポートしているかを取得します。</summary>
    bool CapDepositDataEvent { get; }

    /// <summary>状態検証をスキップするかどうかを取得します。</summary>
    bool SkipStateVerification { get; }

    /// <summary>リアルタイムデータ通知が有効かどうかを取得します。</summary>
    bool RealTimeDataEnabled { get; }

    /// <summary>pOS.NET の内部イベントキューイングを無効化するかどうかを取得します。</summary>
    bool DisableUposEventQueuing { get; }

    /// <summary>非同期処理の実行結果を取得または設定します。</summary>
    int AsyncResultCode { get; set; }

    /// <summary>非同期処理の拡張エラーコードを取得または設定します。</summary>
    int AsyncResultCodeExtended { get; set; }

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
