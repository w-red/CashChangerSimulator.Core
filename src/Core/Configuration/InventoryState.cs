namespace CashChangerSimulator.Core.Configuration;

/// <summary>在庫の現在の枚数を保持するクラス（永続化用）。.</summary>
public class InventoryState
{
    /// <summary>Gets or sets 金種キー（B1000, C100等）と現在の枚数のマップ。.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Needed for TOML deserialization")]
    public Dictionary<string, int> Counts { get; set; } = [];

    /// <summary>インスタンスの状態が整合していることを保証します。.</summary>
    public static void EnsureInitialized()
    {
        // プロパティが読み取り専用で、初期値が空のディクショナリであるため、何もしません。
        // ※後方互換性やインターフェースの実装などの理由で残している可能性がありますが、
        // 少なくとも null チェックは不要になりました。
    }
}
