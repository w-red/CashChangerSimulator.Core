namespace CashChangerSimulator.Core.Configuration;

/// <summary>全般的なシステム設定を保持するクラス。</summary>
public class SystemSettings
{
    /// <summary>現在の通貨コード（例: "JPY", "USD"）。</summary>
    public string CurrencyCode { get; set; } = "JPY";

    /// <summary>カルチャコード（言語設定、例: "ja-JP", "en-US"）。</summary>
    public string CultureCode { get; set; } = "en-US";

    /// <summary>UIの動作モード（Standard, POS）。</summary>
    public UIMode UIMode { get; set; } = UIMode.Standard;

    /// <summary>ベーステーマ（"Dark", "Light"）。</summary>
    public string BaseTheme { get; set; } = "Dark";
}
