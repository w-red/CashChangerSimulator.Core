namespace CashChangerSimulator.Device.Virtual.Services;

/// <summary>スクリプトコマンドのデータモデル。.</summary>
public class ScriptCommand
{
    /// <summary>Gets or sets 操作名を取得または設定します。.</summary>
    public string Op { get; set; } = string.Empty;

    /// <summary>Gets or sets 通貨コードを取得または設定します。.</summary>
    public string? Currency { get; set; }

    /// <summary>Gets or sets 値（数値または変数参照）を取得または設定します。.</summary>
    public object Value { get; set; } = 0;

    /// <summary>Gets or sets 枚数または回数を取得または設定します。.</summary>
    public object? Count { get; set; }

    /// <summary>Gets or sets キャッシュタイプを取得または設定します。.</summary>
    public string? Type { get; set; }

    /// <summary>Gets or sets アクション名を取得または設定します。.</summary>
    public string? Action { get; set; }

    /// <summary>Gets or sets 変数名を取得または設定します。.</summary>
    public string? Variable { get; set; }

    /// <summary>Gets or sets 入れ子になったコマンドリストを取得または設定します。.</summary>
    public IReadOnlyList<ScriptCommand>? Commands { get; set; }

    /// <summary>Gets or sets エラータイプを取得または設定します。.</summary>
    public string? Error { get; set; }

    /// <summary>Gets or sets 検証対象を取得または設定します。.</summary>
    public string? Target { get; set; }

    /// <summary>Gets or sets 額面を取得または設定します。.</summary>
    public object? Denom { get; set; }

    /// <summary>Gets or sets エラー箇所を取得または設定します。.</summary>
    public string? Location { get; set; }

    /// <summary>Gets or sets errorCode を取得または設定します。.</summary>
    public object? ErrorCode { get; set; }

    /// <summary>Gets or sets errorCodeExtended を取得または設定します。.</summary>
    public object? ErrorCodeExtended { get; set; }
}
