namespace CashChangerSimulator.Tests;

/// <summary>ユニットテスト用の共通待機時間定数。</summary>
public static class TestTimingConstants
{
    /// <summary>シミュレーション完了待機時間（ミリ秒）。</summary>
    public const int CompletionWaitMs = 300;

    /// <summary>ディスペンス開始確認待機時間（ミリ秒）。</summary>
    public const int StartupCheckDelayMs = 50;
}
