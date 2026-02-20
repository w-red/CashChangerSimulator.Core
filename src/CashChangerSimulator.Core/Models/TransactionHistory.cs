using CashChangerSimulator.Core.Configuration;
using R3;

namespace CashChangerSimulator.Core.Models;

/// <summary>取引履歴を管理するクラス。</summary>
public class TransactionHistory : IDisposable
{
    private const int MaxEntries = 1000;
    private readonly List<TransactionEntry> _entries = [];
    private readonly Subject<TransactionEntry> _added = new();

    /// <summary>全ての取引履歴（読み取り専用）。</summary>
    public IReadOnlyList<TransactionEntry> Entries => _entries;

    /// <summary>取引が新しく追加されたときに通知されるストリーム。</summary>
    public virtual Observable<TransactionEntry> Added => _added;

    /// <summary>履歴を追加します。</summary>
    /// <param name="entry">追加する履歴エントリ。</param>
    public virtual void Add(TransactionEntry entry)
    {
        _entries.Insert(0, entry); // 最新を先頭に
        if (_entries.Count > MaxEntries)
        {
            _entries.RemoveAt(_entries.Count - 1);
        }
        _added.OnNext(entry);
    }

    /// <summary>永続化用の状態オブジェクトに変換します。</summary>
    public HistoryState ToState()
    {
        return new HistoryState
        {
            Entries = _entries.Select(e => new HistoryEntryState
            {
                Timestamp = e.Timestamp,
                Type = e.Type,
                Amount = e.Amount,
                Counts = e.Counts.ToDictionary(
                    kv => $"{kv.Key.CurrencyCode}:{(kv.Key.Type == MoneyKind4Opos.Currencies.Interfaces.CashType.Bill ? "B" : "C")}{kv.Key.Value}",
                    kv => kv.Value)
            }).ToList()
        };
    }

    /// <summary>永続化用の状態オブジェクトから履歴を復元します。</summary>
    public void FromState(HistoryState state)
    {
        _entries.Clear();
        if (state.Entries == null) return;

        foreach (var s in state.Entries.Take(MaxEntries))
        {
            var counts = new Dictionary<DenominationKey, int>();
            foreach (var kv in s.Counts)
            {
                var parts = kv.Key.Split(':');
                if (parts.Length == 2 && DenominationKey.TryParse(parts[1], parts[0], out var key) && key != null)
                {
                    counts[key] = kv.Value;
                }
            }
            _entries.Add(new TransactionEntry(s.Timestamp, s.Type, s.Amount, counts));
        }
    }

    public void Dispose()
    {
        _added.Dispose();
        GC.SuppressFinalize(this);
    }
}
