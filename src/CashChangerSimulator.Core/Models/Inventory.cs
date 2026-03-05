using Microsoft.Extensions.Logging;
using R3;
using ZLogger;

namespace CashChangerSimulator.Core.Models;

/// <summary>金種ごとの在庫枚数を管理するクラス。</summary>
public class Inventory : IReadOnlyInventory
{
    private readonly ILogger<Inventory> _logger = LogProvider.CreateLogger<Inventory>();
    private readonly Dictionary<DenominationKey, int> _counts = [];
    private readonly Dictionary<DenominationKey, int> _collectionCounts = [];
    private readonly Dictionary<DenominationKey, int> _rejectCounts = [];
    private readonly Subject<DenominationKey> _changed = new();

    /// <inheritdoc/>
    public virtual Observable<DenominationKey> Changed => _changed;

    private bool _isForcedDiscrepancy;

    /// <summary>在庫の不一致が発生しているかどうかを取得または設定します。</summary>
    /// <remarks>通常、回収庫またはリジェクト庫に現金がある場合に不一致と見なされます。手動での設定も可能です。</remarks>
    public virtual bool HasDiscrepancy
    {
        get => _isForcedDiscrepancy || _collectionCounts.Any(kv => kv.Value > 0) || _rejectCounts.Any(kv => kv.Value > 0);
        set => _isForcedDiscrepancy = value;
    }

    /// <summary>通常庫（リサイクル可能）の全枚数。</summary>
    public virtual IEnumerable<KeyValuePair<DenominationKey, int>> AllCounts => _counts;

    /// <summary>回収庫（オーバーフロー等）の全枚数。</summary>
    public virtual IEnumerable<KeyValuePair<DenominationKey, int>> CollectionCounts => _collectionCounts;

    /// <summary>リジェクト庫（汚損等）の全枚数。</summary>
    public virtual IEnumerable<KeyValuePair<DenominationKey, int>> RejectCounts => _rejectCounts;
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

    /// <summary>指定された金種の枚数を回収庫に追加する。</summary>
    public virtual void AddCollection(DenominationKey key, int count)
    {
        if (count <= 0) return;
        if (_collectionCounts.ContainsKey(key))
        {
            _collectionCounts[key] += count;
        }
        else
        {
            _collectionCounts[key] = count;
        }
        _changed.OnNext(key);
    }

    /// <summary>指定された金種の枚数をリジェクト庫に追加する。</summary>
    public virtual void AddReject(DenominationKey key, int count)
    {
        if (count <= 0) return;
        if (_rejectCounts.ContainsKey(key))
        {
            _rejectCounts[key] += count;
        }
        else
        {
            _rejectCounts[key] = count;
        }
        _changed.OnNext(key);
    }

    /// <inheritdoc/>
    public virtual int GetCount(DenominationKey key)
    {
        return _counts.GetValueOrDefault(key, 0);
    }

    /// <summary>現在の在庫の合計金額を計算します。</summary>
    /// <remarks>通常庫、回収庫、リジェクト庫のすべての合計を計算します。</remarks>
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
        foreach (var (key, count) in _collectionCounts)
        {
            if (currencyCode == null || key.CurrencyCode == currencyCode)
            {
                total += key.Value * count;
            }
        }
        foreach (var (key, count) in _rejectCounts)
        {
            if (currencyCode == null || key.CurrencyCode == currencyCode)
            {
                total += key.Value * count;
            }
        }
        return total;
    }


    /// <summary>現在の在庫を保存用のデータ形式に変換します。</summary>
    public Dictionary<string, int> ToDictionary()
    {
        var result = _counts.ToDictionary(
            kv => $"{kv.Key.CurrencyCode}{DenominationKey.KeySeparator}{kv.Key.PrefixChar}{kv.Key.Value}",
            kv => kv.Value
        );
        foreach (var kv in _collectionCounts)
        {
            result[$"COL:{kv.Key.CurrencyCode}{DenominationKey.KeySeparator}{kv.Key.PrefixChar}{kv.Key.Value}"] = kv.Value;
        }
        foreach (var kv in _rejectCounts)
        {
            result[$"REJ:{kv.Key.CurrencyCode}{DenominationKey.KeySeparator}{kv.Key.PrefixChar}{kv.Key.Value}"] = kv.Value;
        }
        return result;
    }

    /// <summary>文字列キーのディクショナリから在庫を復元します。</summary>
    public void LoadFromDictionary(IReadOnlyDictionary<string, int> data)
    {
        foreach (var kv in data)
        {
            try
            {
                if (kv.Key.StartsWith("COL:"))
                {
                    var actualKey = kv.Key[4..];
                    if (TryParseKey(actualKey, out var denKey, out var currency))
                    {
                        AddCollection(denKey, kv.Value);
                    }
                }
                else if (kv.Key.StartsWith("REJ:"))
                {
                    var actualKey = kv.Key[4..];
                    if (TryParseKey(actualKey, out var denKey, out var currency))
                    {
                        AddReject(denKey, kv.Value);
                    }
                }
                else
                {
                    if (TryParseKey(kv.Key, out var denKey, out var currency))
                    {
                        SetCount(denKey, kv.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.ZLogWarning($"Failed to load inventory key: {kv.Key}. Error: {ex.Message}");
            }
        }
    }

    private bool TryParseKey(string fullKey, out DenominationKey? key, out string? currencyCode)
    {
        key = null;
        currencyCode = null;
        var parts = fullKey.Split(DenominationKey.KeySeparator);
        if (parts.Length == 2)
        {
            currencyCode = parts[0];
            var denomStr = parts[1];
            if (DenominationKey.TryParse(denomStr, currencyCode, out var parsedKey) && parsedKey != null)
            {
                key = parsedKey;
                return true;
            }
        }
        return false;
    }
}
