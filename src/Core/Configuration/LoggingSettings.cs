namespace CashChangerSimulator.Core.Configuration;

/// <summary>ロギング設定を管理するクラス。.</summary>
public class LoggingSettings
{
    /// <summary>Gets or sets a value indicating whether コンソール出力を有効にするかどうか。.</summary>
    public bool EnableConsole { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether ファイル出力を有効にするかどうか。.</summary>
    public bool EnableFile { get; set; } = true;

    /// <summary>Gets or sets ログレベル (Debug, Information, Warning, Error)。.</summary>
    public string LogLevel { get; set; } = "Information";

    /// <summary>Gets or sets ログを保存するディレクトリ。.</summary>
    public string LogDirectory { get; set; } = "logs";

    /// <summary>Gets or sets ログファイル名。.</summary>
    public string LogFileName { get; set; } = "app.log";
}
