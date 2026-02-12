using System.Collections.Generic;
using R3;

namespace CashChangerSimulator.Core.Models;

/// <summary>
/// 読み取り専用の在庫情報のインターフェース。
/// </summary>
public interface IReadOnlyInventory
{
    int GetCount(int denomination);
    decimal CalculateTotal();
    Observable<int> Changed { get; }
}

/// <summary>
/// 金種ごとの在庫枚数を管理するクラス。
/// </summary>
public class Inventory : IReadOnlyInventory
{
    private readonly Dictionary<int, int> _counts = new();
    private readonly Subject<int> _changed = new();

    /// <summary>
    /// 在庫が変更されたときに通知されるイベントストリーム。変更された金種（額面）を流す。
    /// </summary>
    public Observable<int> Changed => _changed;

    /// <summary>
    /// 指定された金種の枚数を追加する。
    /// </summary>
    /// <param name="denomination">金種（額面）。</param>
    /// <param name="count">追加する枚数。</param>
    public void Add(int denomination, int count)
    {
        if (_counts.ContainsKey(denomination))
        {
            _counts[denomination] += count;
        }
        else
        {
            _counts[denomination] = count;
        }
        _changed.OnNext(denomination);
    }

    /// <summary>
    /// 指定された金種の枚数を設定する。
    /// </summary>
    /// <param name="denomination">金種（額面）。</param>
    /// <param name="count">設定する枚数。</param>
    public void SetCount(int denomination, int count)
    {
        _counts[denomination] = count;
        _changed.OnNext(denomination);
    }

    /// <summary>
    /// 指定された金種の現在の枚数を取得する。
    /// </summary>
    /// <param name="denomination">金種（額面）。</param>
    /// <returns>現在の枚数。存在しない場合は 0。</returns>
    public int GetCount(int denomination)
    {
        return _counts.GetValueOrDefault(denomination, 0);
    }

    /// <summary>
    /// 現在の在庫の合計金額を計算する。
    /// </summary>
    /// <returns>合計金額。</returns>
    public decimal CalculateTotal()
    {
        decimal total = 0;
        foreach (var (denomination, count) in _counts)
        {
            total += (decimal)denomination * count;
        }
        return total;
    }
}
