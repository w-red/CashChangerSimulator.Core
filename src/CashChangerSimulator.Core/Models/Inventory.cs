using System.Collections.Generic;
using MoneyKind4Opos.Currencies.Interfaces;
using R3;

namespace CashChangerSimulator.Core.Models;

/// <summary>金種を一意に識別するための複合キー（額面と硬貨/紙幣の種別）。</summary>
public record DenominationKey(decimal Value, CashType Type)
{
    /// <summary>
    /// 文字列形式（例: "B10000", "C500"）から DenominationKey を解析する。
    /// </summary>
    /// <param name="s">解析対象の文字列。先頭が 'B' (Bill) または 'C' (Coin) である必要がある。</param>
    /// <param name="result">解析結果のキー。</param>
    /// <returns>解析に成功した場合は true、それ以外は false。</returns>
    public static bool TryParse(string s, out DenominationKey? result)
    {
        result = null;
        if (string.IsNullOrEmpty(s) || s.Length < 2) return false;

        var type = s[0] switch
        {
            'B' or 'b' => CashType.Bill,
            'C' or 'c' => CashType.Coin,
            _ => CashType.Undefined
        };

        if (type == CashType.Undefined) return false;

        if (decimal.TryParse(s[1..], out var value))
        {
            result = new DenominationKey(value, type);
            return true;
        }

        return false;
    }
}

/// <summary>読み取り専用の在庫情報のインターフェース。</summary>
public interface IReadOnlyInventory
{
    /// <summary>指定された金種の現在の枚数を取得する。</summary>
    int GetCount(DenominationKey key);
    /// <summary>現在の在庫の合計金額を計算する。</summary>
    decimal CalculateTotal();
    /// <summary>在庫が変更されたときに通知されるイベントストリーム。</summary>
    Observable<DenominationKey> Changed { get; }
}

/// <summary>金種ごとの在庫枚数を管理するクラス。</summary>
public class Inventory : IReadOnlyInventory
{
    private readonly Dictionary<DenominationKey, int> _counts = new();
    private readonly Subject<DenominationKey> _changed = new();

    /// <inheritdoc/>
    public virtual Observable<DenominationKey> Changed => _changed;

    /// <summary>指定された金種の枚数を追加する。</summary>
    public virtual void Add(DenominationKey key, int count)
    {
        if (_counts.ContainsKey(key))
        {
            _counts[key] += count;
        }
        else
        {
            _counts[key] = count;
        }
        _changed.OnNext(key);
    }

    /// <summary>指定された金種の枚数を設定する。</summary>
    public virtual void SetCount(DenominationKey key, int count)
    {
        _counts[key] = count;
        _changed.OnNext(key);
    }

    /// <summary>指定された金種の現在の枚数を取得する。</summary>
    public virtual int GetCount(DenominationKey key)
    {
        return _counts.GetValueOrDefault(key, 0);
    }

    /// <summary>現在の在庫の合計金額を計算する。</summary>
    public virtual decimal CalculateTotal()
    {
        decimal total = 0;
        foreach (var (key, count) in _counts)
        {
            total += key.Value * count;
        }
        return total;
    }

    /// <summary>全在庫の金種キーと枚数の列挙を取得する。</summary>
    public IEnumerable<KeyValuePair<DenominationKey, int>> AllCounts => _counts;
}
