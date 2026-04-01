using CashChangerSimulator.Core.Transactions;
using MemoryPack;

namespace CashChangerSimulator.Core.Configuration;

/// <summary>取引履歴の状態を保持するクラス（永続化用）。</summary>
[MemoryPackable]
public partial class HistoryState
{
    /// <summary>取引履歴エントリのリスト。</summary>
    public List<TransactionEntry> Entries { get; set; } = [];

    [MemoryPackOnDeserialized]
    void OnDeserialized()
    {
        Entries ??= [];
    }
}
