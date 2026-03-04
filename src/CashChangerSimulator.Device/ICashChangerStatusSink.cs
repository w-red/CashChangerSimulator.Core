using System;

namespace CashChangerSimulator.Device;

/// <summary>デバイスの状態変更通知を受け取るためのシンクインターフェース。</summary>
public interface ICashChangerStatusSink
{
    /// <summary>UPOS イベントを発生させます。</summary>
    void FireEvent(EventArgs e);

    /// <summary>非同期処理中かどうかを設定します。</summary>
    void SetAsyncProcessing(bool isBusy);

    /// <summary>データイベントが有効かどうか。</summary>
    bool DataEventEnabled { get; }

    /// <summary>リアルタイムデータ通知が有効かどうか。</summary>
    bool RealTimeDataEnabled { get; }
}
