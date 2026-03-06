using System;

namespace CashChangerSimulator.Device.Services;

/// <summary>UPOS デバイスからのイベント通知を管理・抽象化するインターフェース。</summary>
public interface IUposEventNotifier
{
    /// <summary>イベントを通知キューに追加（または即時送信）します。</summary>
    /// <param name="e">イベント引数。</param>
    void NotifyEvent(System.EventArgs e);

    /// <summary>外部からイベントを強制的に発生させます。</summary>
    /// <param name="e">イベント引数。</param>
    void FireEvent(System.EventArgs e);
}

/// <summary>イベント通知の送り先となるインターフェース。</summary>
public interface IUposEventSink
{
    /// <summary>DataEvent が有効かどうか。</summary>
    bool DataEventEnabled { get; }

    /// <summary>入金データイベントをサポートしているか。</summary>
    bool CapDepositDataEvent { get; }

    /// <summary>状態検証をスキップするかどうか。</summary>
    bool SkipStateVerification { get; }

    /// <summary>イベントを通知し、必要に応じてキューに追加します。</summary>
    /// <param name="e">イベント引数。</param>
    void NotifyEvent(System.EventArgs e);

    /// <summary>イベントをキューに追加します。</summary>
    /// <param name="e">イベント引数。</param>
    void QueueEvent(System.EventArgs e);
}
