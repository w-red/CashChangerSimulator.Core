using CashChangerSimulator.Core.Models;
using MemoryPack;

namespace CashChangerSimulator.Core.Configuration;

/// <summary>取引履歴の状態を保持するクラス（永続化用）。</summary>
[MemoryPackable]
public partial class HistoryState
{
    /// <summary>取引履歴エントリのリスト。</summary>
    public List<HistoryEntryState> Entries { get; set; } = [];

    [MemoryPackOnDeserialized]
    void OnDeserialized()
    {
        Entries ??= [];
    }
}

/// <summary>単一の取引履歴エントリの状態を保持するクラス。</summary>
[MemoryPackable]
public partial class HistoryEntryState
{
    /// <summary>取引日時。</summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>取引種別。</summary>
    public TransactionType Type { get; set; }

    /// <summary>合計金額の変動量。</summary>
    public decimal Amount { get; set; }

    /// <summary>金種ごとの枚数変動。キーは "CurrencyCode:B1000" 形式。</summary>
    public Dictionary<string, int> Counts { get; set; } = [];

    [MemoryPackOnDeserialized]
    void OnDeserialized()
    {
        Counts ??= [];
    }
}
