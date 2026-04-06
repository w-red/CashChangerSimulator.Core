using CashChangerSimulator.Core.Transactions;
using MemoryPack;

namespace CashChangerSimulator.Core.Configuration;

/// <summary>取引履歴の状態を保持するクラス（永続化用）。.</summary>
[MemoryPackable]
public partial class HistoryState
{
    private List<TransactionEntry> entries = [];

    /// <summary>Gets or sets 取引履歴エントリのリスト。.</summary>
    public IReadOnlyList<TransactionEntry> Entries
    {
        get => entries;
        set => entries = value == null ? [] : (value is List<TransactionEntry> list ? list : [.. value]);
    }

    [MemoryPackOnDeserialized]
    private void OnDeserialized()
    {
        entries ??= [];
    }
}
