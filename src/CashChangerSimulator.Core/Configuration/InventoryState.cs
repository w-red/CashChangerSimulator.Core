using CsToml;

namespace CashChangerSimulator.Core.Configuration;

/// <summary>
/// 在庫の現在の枚数を保持するクラス（永続化用）。
/// </summary>
[TomlSerializedObject]
public partial class InventoryState
{
    /// <summary>金種キー（B1000, C100等）と現在の枚数のマップ。</summary>
    [TomlValueOnSerialized]
    public Dictionary<string, int> Counts { get; set; } = [];

    public void EnsureInitialized()
    {
        Counts ??= [];
    }
}
