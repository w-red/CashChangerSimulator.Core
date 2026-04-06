using Microsoft.PointOfService;
using R3;

namespace CashChangerSimulator.Device.PosForDotNet.Services;

/// <summary>イベント通知の送り先となる SO 本体が実装するインターフェース。.</summary>
/// <remarks>
/// Microsoft POS for .NET (OPOS) の基本クラス（CashChangerBasic等）が持つイベント通知メソッドへの橋渡しを行います。.
/// </remarks>
public interface IUposEventSink
{
    /// <summary>Gets デバイスの状態（POS for .NET 標準）。.</summary>
    ControlState State { get; }

    /// <summary>Gets or sets a value indicating whether デバイスが占有されているかどうか。.</summary>
    bool Claimed { get; set; }

    /// <summary>Gets or sets a value indicating whether 他プロセスで占有されているかどうか。.</summary>
    bool ClaimedByAnother { get; set; }

    /// <summary>Gets or sets a value indicating whether デバイスが有効化されているかどうか。.</summary>
    bool DeviceEnabled { get; set; }

    /// <summary>Gets a value indicating whether dataEvent が有効かどうか。.</summary>
    bool DataEventEnabled { get; }

    /// <summary>Gets a value indicating whether 入金データイベントをサポートしているか。.</summary>
    bool CapDepositDataEvent { get; }

    /// <summary>Gets a value indicating whether 状態検証をスキップするかどうか。.</summary>
    bool SkipStateVerification { get; }

    /// <summary>Gets a value indicating whether リアルタイムデータ通知が有効かどうか。.</summary>
    bool RealTimeDataEnabled { get; }

    /// <summary>Gets a value indicating whether pOS.NET の内部イベントキューイングを無効化するかどうかを取得します。.</summary>
    bool DisableUposEventQueuing { get; }

    /// <summary>Gets or sets 非同期処理の実行結果。.</summary>
    int AsyncResultCode { get; set; }

    /// <summary>Gets or sets 非同期処理の拡張エラーコード。.</summary>
    int AsyncResultCodeExtended { get; set; }

    /// <summary>イベントを通知し、必要に応じてキューに追加します。.</summary>
    /// <param name="e">イベント引数。.</param>
    void NotifyEvent(EventArgs e);

    /// <summary>イベントをキューに追加します。.</summary>
    /// <param name="e">イベント引数。.</param>
    void QueueEvent(EventArgs e);

    /// <summary>データイベントをキューに追加します。.</summary>
    /// <param name="e">イベント引数。.</param>
    void QueueDataEvent(DataEventArgs e);

    /// <summary>Gets 入金状態の変更を通知するストリームを取得します。.</summary>
    Observable<Unit> DepositChanged { get; }

    /// <summary>ステータス更新イベントをキューに追加します。.</summary>
    /// <param name="e">イベント引数。.</param>
    void QueueStatusUpdateEvent(StatusUpdateEventArgs e);
}
