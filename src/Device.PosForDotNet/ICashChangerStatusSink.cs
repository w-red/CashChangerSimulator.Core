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

    /// <summary>デバイスが占有されているかどうかを取得または設定します。</summary>
    bool Claimed { get; set; }

    /// <summary>他プロセスで占有されているかどうかを取得または設定します。</summary>
    bool ClaimedByAnother { get; set; }

    /// <summary>デバイスが有効化されているかどうかを取得または設定します。</summary>
    bool DeviceEnabled { get; set; }

    /// <summary>データイベントが有効かどうかを取得します。</summary>
    bool DataEventEnabled { get; }

    /// <summary>リアルタイムデータ通知が有効かどうかを取得します。</summary>
    bool RealTimeDataEnabled { get; }

    /// <summary>非同期処理の実行結果を取得または設定します。</summary>
    int AsyncResultCode { get; set; }

    /// <summary>非同期処理の拡張エラーコードを取得または設定します。</summary>
    int AsyncResultCodeExtended { get; set; }
}
