using CashChangerSimulator.Core.Configuration;
using R3;

namespace CashChangerSimulator.Core.Transactions;

/// <summary>取引履歴を管理するクラス。</summary>
public class TransactionHistory : IDisposable
{
    private readonly int _maxEntries;
    private readonly List<TransactionEntry> _entries = [];
    private readonly Subject<TransactionEntry> _added = new();

    public TransactionHistory(SimulatorConfiguration config)
    {
        _maxEntries = config.History.MaxEntries;
    }

    public TransactionHistory() : this(new SimulatorConfiguration()) { }

    /// <summary>全ての取引履歴（読み取り専用）。</summary>
    public virtual IReadOnlyList<TransactionEntry> Entries => _entries;

    /// <summary>取引が新しく追加されたときに通知されるストリーム。</summary>
    public virtual Observable<TransactionEntry> Added => _added;

    /// <summary>履歴を追加します。</summary>
    /// <param name="entry">追加する履歴エントリ。</param>
    public virtual void Add(TransactionEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _entries.Insert(0, entry); // 最新を先頭に
        if (_entries.Count > _maxEntries)
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
            Entries = [.. _entries]
        };
    }

    /// <summary>永続化用の状態オブジェクトから履歴を復元します。</summary>
    public void FromState(HistoryState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _entries.Clear();
        if (state.Entries == null) return;

        foreach (var entry in state.Entries.Take(_maxEntries))
        {
            _entries.Add(entry);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _added.Dispose();
        GC.SuppressFinalize(this);
    }
}
