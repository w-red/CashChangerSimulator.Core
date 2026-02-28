namespace CashChangerSimulator.Core.Configuration;

/// <summary>在庫の現在の枚数を保持するクラス（永続化用）。</summary>
public class InventoryState
{
    /// <summary>金種キー（B1000, C100等）と現在の枚数のマップ。</summary>
    public Dictionary<string, int> Counts { get; set; } = [];

    /// <summary>インスタンスの状態が整合していることを保証します。</summary>
    public void EnsureInitialized()
    {
        Counts ??= [];
    }
}
