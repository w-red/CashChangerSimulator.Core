using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.PosForDotNet;

/// <summary>デバイスの状態変更通知を受け取るためのシンクインターフェース。.</summary>
public interface ICashChangerStatusSink
{
    /// <summary>UPOS イベントを発生させます。.</summary>
    void FireEvent(EventArgs e);

    /// <summary>非同期処理中かどうかを設定します。.</summary>
    void SetAsyncProcessing(bool isBusy);

    /// <summary>Gets デバイスの状態（POS for .NET 標準）。.</summary>
    ControlState State { get; }

    /// <summary>Gets or sets a value indicating whether デバイスが占有されているかどうか。.</summary>
    bool Claimed { get; set; }

    /// <summary>Gets or sets a value indicating whether 他プロセスで占有されているかどうか。.</summary>
    bool ClaimedByAnother { get; set; }

    /// <summary>Gets or sets a value indicating whether デバイスが有効化されているかどうか。.</summary>
    bool DeviceEnabled { get; set; }

    /// <summary>Gets a value indicating whether データイベントが有効かどうか。.</summary>
    bool DataEventEnabled { get; }

    /// <summary>Gets a value indicating whether リアルタイムデータ通知が有効かどうか。.</summary>
    bool RealTimeDataEnabled { get; }

    /// <summary>Gets or sets 非同期処理の実行結果。.</summary>
    int AsyncResultCode { get; set; }

    /// <summary>Gets or sets 非同期処理の拡張エラーコード。.</summary>
    int AsyncResultCodeExtended { get; set; }
}
