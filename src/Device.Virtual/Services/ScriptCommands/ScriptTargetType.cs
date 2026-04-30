namespace CashChangerSimulator.Device.Virtual.Services.ScriptCommands;

/// <summary>スクリプトの操作対象を表す列挙型クラス。</summary>
public sealed record ScriptTargetType
{
    /// <summary>在庫対象。</summary>
    public static readonly ScriptTargetType Inventory = new("INVENTORY");

    /// <summary>ステータス対象。</summary>
    public static readonly ScriptTargetType Status = new("STATUS");

    /// <summary>対象名(大文字)。</summary>
    public string Name { get; }

    private ScriptTargetType(string name) => Name = name;

    /// <inheritdoc/>
    public override string ToString() => Name;

    /// <summary>文字列から ScriptTargetType を取得します。</summary>
    /// <param name="target">対象名の文字列。</param>
    /// <returns>対応する ScriptTargetType インスタンス。</returns>
    public static ScriptTargetType FromString(string? target)
    {
        if (string.IsNullOrEmpty(target)) return new ScriptTargetType(string.Empty);

        var normalized = target
            .ToUpperInvariant();
        return normalized switch
        {
            "INVENTORY" => Inventory,
            "STATUS" => Status,
            _ => new ScriptTargetType(normalized)
        };
    }
}
