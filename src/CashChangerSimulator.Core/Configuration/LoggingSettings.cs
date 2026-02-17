using CsToml;

namespace CashChangerSimulator.Core.Configuration;

/// <summary>
/// ロギング設定を管理するクラス。
/// </summary>
[TomlSerializedObject]
public partial class LoggingSettings
{
    /// <summary>コンソール出力を有効にするかどうか。</summary>
    [TomlValueOnSerialized]
    public bool EnableConsole { get; set; } = true;

    /// <summary>ファイル出力を有効にするかどうか。</summary>
    [TomlValueOnSerialized]
    public bool EnableFile { get; set; } = true;

    /// <summary>ログレベル (Debug, Information, Warning, Error)。</summary>
    [TomlValueOnSerialized]
    public string LogLevel { get; set; } = "Information";

    /// <summary>ログを保存するディレクトリ。</summary>
    [TomlValueOnSerialized]
    public string LogDirectory { get; set; } = "logs";

    /// <summary>ログファイル名。</summary>
    [TomlValueOnSerialized]
    public string LogFileName { get; set; } = "app.log";
}
