using Microsoft.Extensions.Logging;
using R3;
using ZLogger;

namespace CashChangerSimulator.Core.Models;

/// <summary>金種ごとの在庫枚数を管理するクラス。</summary>
public class Inventory : IReadOnlyInventory
{
    private readonly ILogger<Inventory> _logger = LogProvider.CreateLogger<Inventory>();
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
            key = key with { CurrencyCode = DenominationKey.DefaultCurrencyCode };
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

    /// <summary>現在の在庫を保存用のデータ形式に変換します。</summary>
    public Dictionary<string, int> ToDictionary()
    {
        return _counts.ToDictionary(
            kv => $"{kv.Key.CurrencyCode}{DenominationKey.KeySeparator}{kv.Key.PrefixChar}{kv.Key.Value}",
            kv => kv.Value
        );
    }

    /// <summary>文字列キーのディクショナリから在庫を復元します。</summary>
    public void LoadFromDictionary(IReadOnlyDictionary<string, int> data)
    {
        foreach (var kv in data)
        {
            // "JPY:B1000" 形式を想定
            var parts = kv.Key.Split(DenominationKey.KeySeparator);
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
