namespace CashChangerSimulator.Device.Virtual.Services;

/// <summary>スクリプトコマンドのデータモデル。</summary>
public class ScriptCommand
{
    /// <summary>操作名を取得または設定します。</summary>
    public string Op { get; set; } = string.Empty;

    /// <summary>通貨コードを取得または設定します。</summary>
    public string? Currency { get; set; }

    /// <summary>値（数値または変数参照）を取得または設定します。</summary>
    public object Value { get; set; } = 0;

    /// <summary>枚数または回数を取得または設定します。</summary>
    public object? Count { get; set; }

    /// <summary>キャッシュタイプを取得または設定します。</summary>
    public string? Type { get; set; }

    /// <summary>アクション名を取得または設定します。</summary>
    public string? Action { get; set; }

    /// <summary>変数名を取得または設定します。</summary>
    public string? Variable { get; set; }

    /// <summary>入れ子になったコマンドリストを取得または設定します。</summary>
    public IReadOnlyList<ScriptCommand>? Commands { get; set; }

    /// <summary>エラータイプを取得または設定します。</summary>
    public string? Error { get; set; }

    /// <summary>検証対象を取得または設定します。</summary>
    public string? Target { get; set; }

    /// <summary>額面を取得または設定します。</summary>
    public object? Denom { get; set; }

    /// <summary>エラー箇所を取得または設定します。</summary>
    public string? Location { get; set; }

    /// <summary>エラーコードを取得または設定します。</summary>
    public object? ErrorCode { get; set; }

    /// <summary>拡張エラーコードを取得または設定します。</summary>
    public object? ErrorCodeExtended { get; set; }
}
