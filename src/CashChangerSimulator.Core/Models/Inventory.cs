using MoneyKind4Opos.Currencies.Interfaces;
using Microsoft.Extensions.Logging;
using R3;
using ZLogger;

namespace CashChangerSimulator.Core.Models;

/// <summary>金種を一意に識別するための複合キー（通貨コード、額面、硬貨/紙幣の種別）。</summary>
public record DenominationKey(decimal Value, CashType Type, string CurrencyCode = "JPY")
{
    /// <summary>文字列形式（例: "B10000", "C500"）から DenominationKey を解析します。</summary>
    /// <param name="s">解析対象の文字列。先頭が 'B' (Bill) または 'C' (Coin) である必要がある。</param>
    /// <param name="result">解析結果のキー。</param>
    /// <returns>解析に成功した場合は true、それ以外は false。</returns>
    public static bool TryParse(string s, out DenominationKey? result)
    {
        return TryParse(s, "JPY", out result);
    }

    /// <summary>通貨コードと文字列形式から DenominationKey を解析します。</summary>
    public static bool TryParse(string s, string currencyCode, out DenominationKey? result)
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
            result = new DenominationKey(value, type, currencyCode);
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
    decimal CalculateTotal(string? currencyCode = null);
    /// <summary>在庫が変更されたときに通知されるイベントストリーム。</summary>
    Observable<DenominationKey> Changed { get; }
}

/// <summary>金種ごとの在庫枚数を管理するクラス。</summary>
public class Inventory : IReadOnlyInventory
{
    private readonly Microsoft.Extensions.Logging.ILogger<Inventory> _logger = LogProvider.CreateLogger<Inventory>();
    private readonly Dictionary<DenominationKey, int> _counts = [];
    private readonly Subject<DenominationKey> _changed = new();

    /// <inheritdoc/>
    public virtual Observable<DenominationKey> Changed => _changed;

    /// <summary>指定された金種の枚数を追加する。</summary>
    public virtual void Add(DenominationKey key, int count)
    {
        // Normalize key if meaningful currency code is missing
        if (string.IsNullOrEmpty(key.CurrencyCode))
        {
            key = key with { CurrencyCode = "JPY" };
        }
        _logger.ZLogDebug($"Inventory.Add called. Key: {key}, Count: {count}");
        if (_counts.ContainsKey(key))
        {
            _counts[key] += count;
        }
        else
        {
            _counts[key] = count;
        }
        _changed.OnNext(key);
        _logger.ZLogDebug($"Inventory.Add finished. New Total: {CalculateTotal()}");
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
    public virtual decimal CalculateTotal(string? currencyCode = null)
    {
        decimal total = 0;
        foreach (var (key, count) in _counts)
        {
            if (currencyCode == null || key.CurrencyCode == currencyCode)
            {
                total += key.Value * count;
            }
        }
        return total;
    }

    /// <summary>全在庫の金種キーと枚数の列挙を取得する。</summary>
    public IEnumerable<KeyValuePair<DenominationKey, int>> AllCounts => _counts;

    /// <summary>現在の在庫を保存用のディクショナリ（"CurrencyCode:TypeAmount" 形式）に変換します。</summary>
    public Dictionary<string, int> ToDictionary()
    {
        return _counts.ToDictionary(
            kv => $"{kv.Key.CurrencyCode}:{(kv.Key.Type == CashType.Bill ? "B" : "C")}{kv.Key.Value}",
            kv => kv.Value
        );
    }

    /// <summary>文字列キーのディクショナリから在庫を復元します。</summary>
    public void LoadFromDictionary(IReadOnlyDictionary<string, int> data)
    {
        foreach (var kv in data)
        {
            // "JPY:B1000" 形式を想定
            var parts = kv.Key.Split(':');
            if (parts.Length == 2)
            {
                var currencyCode = parts[0];
                var denomStr = parts[1];
                if (DenominationKey.TryParse(denomStr, currencyCode, out var key) && key != null)
                {
                    SetCount(key, kv.Value);
                }
            }
            else
            {
                _logger.ZLogWarning($"Invalid inventory key format: {kv.Key}");
            }
        }
    }
}
