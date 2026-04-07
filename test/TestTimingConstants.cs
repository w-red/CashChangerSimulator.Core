namespace CashChangerSimulator.Tests;

/// <summary>ユニットテスト用の共通待機時間定数。</summary>
internal static class TestTimingConstants
{
    /// <summary>シミュレーション完了待機時間（ミリ秒）。</summary>
    public const int CompletionWaitMs = 500;

    /// <summary>デフォルトのタイムアウト時間（ミリ秒）。CI環境等の負荷を考慮。</summary>
    public const int DefaultTimeoutMs = 1000;

    /// <summary>標準的な短い待機時間（ミリ秒）。</summary>
    public const int ShortDelayMs = 100;

    /// <summary>ディスペンス開始確認待機時間（ミリ秒）。</summary>
    public const int StartupCheckDelayMs = 50;

    /// <summary>UI状態遷移待機時間（ミリ秒）。</summary>
    public const int UiTransitionDelayMs = 1000;

    /// <summary>ウィンドウポップアップ待機時間（ミリ秒）。</summary>
    public const int WindowPopupDelayMs = 2000;

    /// <summary>UI論理実行待機時間（ミリ秒）。</summary>
    public const int LogicExecutionDelayMs = 500;

    /// <summary>イベント通知の伝播待機時間（ミリ秒）。</summary>
    public const int EventPropagationDelayMs = 100;

    /// <summary>アプリケーション終了・クリーンアップ待機時間（ミリ秒）。</summary>
    public const int AppCleanupDelayMs = 1000;
}
