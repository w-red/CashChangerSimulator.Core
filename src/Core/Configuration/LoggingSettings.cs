namespace CashChangerSimulator.Core.Configuration;

/// <summary>ロギング設定を管理するクラス。</summary>
public class LoggingSettings
{
    /// <summary>コンソール出力を有効にするかどうか。</summary>
    public bool EnableConsole { get; set; } = true;

    /// <summary>ファイル出力を有効にするかどうか。</summary>
    public bool EnableFile { get; set; } = true;

    /// <summary>ログレベル (Debug, Information, Warning, Error)。</summary>
    public string LogLevel { get; set; } = "Information";

    /// <summary>ログを保存するディレクトリ。</summary>
    public string LogDirectory { get; set; } = "logs";

    /// <summary>ログファイル名。</summary>
    public string LogFileName { get; set; } = "app.log";
}
