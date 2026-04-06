using CashChangerSimulator.Core.Configuration;
using R3;

namespace CashChangerSimulator.Core.Transactions;

/// <summary>取引履歴を管理するクラス。.</summary>
public class TransactionHistory : IDisposable
{
    private readonly int maxEntries;
    private readonly List<TransactionEntry> entries = [];
    private readonly Subject<TransactionEntry> added = new();

    /// <summary>Initializes a new instance of the <see cref="TransactionHistory"/> class.設定を指定して取引履歴を初期化します。.</summary>
    /// <param name="config">シミュレータ設定。.</param>
    public TransactionHistory(SimulatorConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);
        maxEntries = config.History.MaxEntries;
    }

    /// <summary>Initializes a new instance of the <see cref="TransactionHistory"/> class.デフォルト設定で取引履歴を初期化します。.</summary>
    public TransactionHistory()
        : this(new SimulatorConfiguration())
    {
    }

    /// <summary>Gets 全ての取引履歴（読み取り専用）。.</summary>
    public virtual IReadOnlyList<TransactionEntry> Entries => entries;

    /// <summary>Gets 取引が新しく追加されたときに通知されるストリーム。.</summary>
    public virtual Observable<TransactionEntry> Added => added;

    /// <summary>履歴を追加します。.</summary>
    /// <param name="entry">追加する履歴エントリ。.</param>
    public virtual void Add(TransactionEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        entries.Insert(0, entry); // 最新を先頭に
        if (entries.Count > maxEntries)
        {
            entries.RemoveAt(entries.Count - 1);
        }

        added.OnNext(entry);
    }

    /// <summary>永続化用の状態オブジェクトに変換します。.</summary>
    /// <returns>永続化用の状態オブジェクト。.</returns>
    public HistoryState ToState()
    {
        return new HistoryState
        {
            Entries = [.. entries],
        };
    }

    /// <summary>永続化用の状態オブジェクトから履歴を復元します。.</summary>
    /// <param name="state">復元元の状態オブジェクト。.</param>
    public void FromState(HistoryState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        entries.Clear();
        if (state.Entries == null)
        {
            return;
        }

        foreach (var entry in state.Entries.Take(maxEntries))
        {
            entries.Add(entry);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>リソースを破棄します。.</summary>
    /// <param name="disposing">マネージリソースを破棄する場合は true。.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            added.Dispose();
        }
    }
}
