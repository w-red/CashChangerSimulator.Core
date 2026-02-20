using CsToml;

namespace CashChangerSimulator.Core.Configuration;

/// <summary>シミュレーターのUIモード</summary>
public enum UIMode
{
    Standard,
    PosTransaction
}

/// <summary>シミュレーション挙動（遅延・エラー）の設定クラス。</summary>
[TomlSerializedObject]
public partial class SimulationSettings
{
    /// <summary>処理遅延を有効にするか。</summary>
    [TomlValueOnSerialized]
    public bool DelayEnabled { get; set; } = false;

    /// <summary>最小遅延時間 (ミリ秒)。</summary>
    [TomlValueOnSerialized]
    public int MinDelayMs { get; set; } = 500;

    /// <summary>最大遅延時間 (ミリ秒)。</summary>
    [TomlValueOnSerialized]
    public int MaxDelayMs { get; set; } = 2000;

    /// <summary>ランダムエラーを有効にするか。</summary>
    [TomlValueOnSerialized]
    public bool RandomErrorsEnabled { get; set; } = false;

    /// <summary>エラー発生確率 (0-100)。</summary>
    [TomlValueOnSerialized]
    public int ErrorRate { get; set; } = 10;

    /// <summary>重なり等による読取エラーの発生確率 (0-100)。</summary>
    [TomlValueOnSerialized]
    public int ValidationFailureRate { get; set; } = 5;

    /// <summary>UIの動作モード。</summary>
    [TomlValueOnSerialized]
    public UIMode UIMode { get; set; } = UIMode.Standard;
}
