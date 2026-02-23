namespace CashChangerSimulator.Tests;

/// <summary>ユニットテスト用の共通待機時間定数。</summary>
public static class TestTimingConstants
{
    /// <summary>シミュレーション完了待機時間（ミリ秒）。</summary>
    public const int CompletionWaitMs = 300;

    /// <summary>ディスペンス開始確認待機時間（ミリ秒）。</summary>
    public const int StartupCheckDelayMs = 50;

    /// <summary>UI状態遷移待機時間（ミリ秒）。</summary>
    public const int UiTransitionDelayMs = 1000;

    /// <summary>ウィンドウポップアップ待機時間（ミリ秒）。</summary>
    public const int WindowPopupDelayMs = 2000;

    /// <summary>UI論理実行待機時間（ミリ秒）。</summary>
    public const int LogicExecutionDelayMs = 500;


}
