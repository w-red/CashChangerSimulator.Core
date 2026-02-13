using System;
using System.Collections.Generic;
using R3;

namespace CashChangerSimulator.Core.Models;

/// <summary>
/// 取引履歴を管理するクラス。
/// </summary>
public class TransactionHistory : IDisposable
{
    private readonly List<TransactionEntry> _entries = new();
    private readonly Subject<TransactionEntry> _added = new();

    /// <summary>
    /// 全ての取引履歴（読み取り専用）。
    /// </summary>
    public IReadOnlyList<TransactionEntry> Entries => _entries;

    /// <summary>
    /// 取引が新しく追加されたときに通知されるストリーム。
    /// </summary>
    public virtual Observable<TransactionEntry> Added => _added;

    /// <summary>
    /// 履歴を追加する。
    /// </summary>
    /// <param name="entry">追加する履歴エントリ。</param>
    public virtual void Add(TransactionEntry entry)
    {
        _entries.Add(entry);
        _added.OnNext(entry);
    }

    public void Dispose()
    {
        _added.Dispose();
    }
}
