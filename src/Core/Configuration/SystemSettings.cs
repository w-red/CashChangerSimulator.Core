namespace CashChangerSimulator.Core.Configuration;

/// <summary>全般的なシステム設定を保持するクラス。</summary>
public class SystemSettings
{
    /// <summary>Gets or sets 現在の通貨コード（例: "JPY", "USD"）。</summary>
    public string CurrencyCode { get; set; } = "JPY";

    /// <summary>Gets or sets カルチャコード（言語設定、例: "ja-JP", "en-US"）。</summary>
    public string CultureCode { get; set; } = "en-US";

    /// <summary>Gets or sets ベーステーマ（"Dark", "Light"）。</summary>
    public string BaseTheme { get; set; } = "Dark";
}
