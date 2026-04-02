using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.PosForDotNet;

/// <summary>デバイスの状態変更通知を受け取るためのシンクインターフェース。</summary>
public interface ICashChangerStatusSink
{
    /// <summary>UPOS イベントを発生させます。</summary>
    void FireEvent(EventArgs e);

    /// <summary>非同期処理中かどうかを設定します。</summary>
    void SetAsyncProcessing(bool isBusy);

    /// <summary>デバイスの状態（POS for .NET 標準）。</summary>
    ControlState State { get; }

    /// <summary>デバイスが占有されているかどうか。</summary>
    bool Claimed { get; set; }

    /// <summary>他プロセスで占有されているかどうか。</summary>
    bool ClaimedByAnother { get; set; }

    /// <summary>デバイスが有効化されているかどうか。</summary>
    bool DeviceEnabled { get; set; }

    /// <summary>データイベントが有効かどうか。</summary>
    bool DataEventEnabled { get; }

    /// <summary>リアルタイムデータ通知が有効かどうか。</summary>
    bool RealTimeDataEnabled { get; }
}
