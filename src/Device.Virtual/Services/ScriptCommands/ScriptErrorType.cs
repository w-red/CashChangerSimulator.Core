namespace CashChangerSimulator.Device.Virtual.Services.ScriptCommands;

/// <summary>スクリプトで注入するエラー種別を表す列挙型クラス。</summary>
public sealed record ScriptErrorType
{
    /// <summary>ジャムエラー。</summary>
    public static readonly ScriptErrorType Jam = new("JAM");

    /// <summary>重なりエラー。</summary>
    public static readonly ScriptErrorType Overlap = new("OVERLAP");

    /// <summary>デバイスエラー。</summary>
    public static readonly ScriptErrorType Device = new("DEVICE");

    /// <summary>エラーなし（リセット）。</summary>
    public static readonly ScriptErrorType None = new("NONE");

    /// <summary>リセット。</summary>
    public static readonly ScriptErrorType Reset = new("RESET");

    /// <summary>エラー名（大文字）。</summary>
    public string Name { get; }

    private ScriptErrorType(string name) => Name = name;

    /// <inheritdoc/>
    public override string ToString() => Name;

    /// <summary>文字列から ScriptErrorType を取得します。</summary>
    /// <param name="error">エラー名の文字列。</param>
    /// <returns>対応する ScriptErrorType インスタンス。</returns>
    public static ScriptErrorType FromString(string? error)
    {
        if (string.IsNullOrEmpty(error)) return None;

        var normalized = error.ToUpperInvariant();
        return normalized switch
        {
            "JAM" => Jam,
            "OVERLAP" => Overlap,
            "DEVICE" => Device,
            "NONE" => None,
            "RESET" => Reset,
            _ => new ScriptErrorType(normalized)
        };
    }
}
