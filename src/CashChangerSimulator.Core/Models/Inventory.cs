using Microsoft.Extensions.Logging;
using R3;
using ZLogger;

namespace CashChangerSimulator.Core.Models;

/// <summary>金種ごとの在庫枚数を管理する実体クラス。</summary>
/// <remarks>
/// 通常庫（リサイクル用）、回収庫（オーバーフロー用）、およびリジェクト庫（汚損・不明用）の
/// 3つの論理的なバケットで現金の枚数を管理します。
/// 在庫の変化は <see cref="Changed"/> ストリームを通じてリアクティブに通知されます。
/// </remarks>
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
        get => _isForcedDiscrepancy || _collectionCounts.Values.Any(v => v > 0) || _rejectCounts.Values.Any(v => v > 0);
        set => _isForcedDiscrepancy = value;
    }

    /// <summary>通常庫（リサイクル可能）の全枚数。</summary>
    public virtual IEnumerable<KeyValuePair<DenominationKey, int>> AllCounts => _counts;

    /// <summary>回収庫（オーバーフロー等）の全枚数。</summary>
    public virtual IEnumerable<KeyValuePair<DenominationKey, int>> CollectionCounts => _collectionCounts;

    /// <summary>リジェクト庫（汚損等）の全枚数。</summary>
    public virtual IEnumerable<KeyValuePair<DenominationKey, int>> RejectCounts => _rejectCounts;
    /// <summary>指定された金種の枚数を追加します。</summary>
    /// <remarks>
    /// 金種キーを正規化し、指定された枚数を通常庫に加算します。
    /// 正負両方の値を許容しますが、最終的な在庫数が負にならないよう内部で正規化されます。
    /// </remarks>
    public virtual void Add(DenominationKey key, int count)
    {
        key = NormalizeKey(key);
        _logger.ZLogDebug($"Inventory.Add called. Key: {key}, Count: {count}");
        if (count == 0)
        {
            return;
        }

        var current = GetCount(key);
        var next = current + count;
        if (next < 0)
        {
            _logger.ZLogWarning($"Inventory.Add: Resulting count for {key} is negative ({next}). Setting to 0.");
            next = 0;
        }

        _counts[key] = next;
        _changed.OnNext(key);
        _logger.ZLogDebug($"Inventory.Add finished. New Total: {CalculateTotal()}");
    }

    /// <summary>指定された金種の枚数を上書き設定します。</summary>
    /// <remarks>
    /// 既存の値を破棄し、指定された枚数で通常庫を更新します。
    /// 不の枚数が指定された場合は 0 として扱われます。
    /// </remarks>
    public virtual void SetCount(DenominationKey key, int count)
    {
        key = NormalizeKey(key);
        if (count < 0)
        {
            _logger.ZLogWarning($"Inventory.SetCount: Ignoring negative count {count} for {key}");
            return;
        }
        _counts[key] = count;
        _changed.OnNext(key);
    }

    /// <summary>指定された金種の枚数を回収庫に追加する。</summary>
    public virtual void AddCollection(DenominationKey key, int count)
    {
        key = NormalizeKey(key);
        if (count == 0) return;

        var current = _collectionCounts.GetValueOrDefault(key, 0);
        var next = current + count;
        if (next < 0)
        {
            _logger.ZLogWarning($"Inventory.AddCollection: Resulting count for {key} is negative ({next}). Setting to 0.");
            next = 0;
        }

        _collectionCounts[key] = next;
        _changed.OnNext(key);
    }

    /// <summary>指定された金種の枚数をリジェクト庫に追加する。</summary>
    public virtual void AddReject(DenominationKey key, int count)
    {
        key = NormalizeKey(key);
        if (count == 0) return;

        var current = _rejectCounts.GetValueOrDefault(key, 0);
        var next = current + count;
        if (next < 0)
        {
            _logger.ZLogWarning($"Inventory.AddReject: Resulting count for {key} is negative ({next}). Setting to 0.");
            next = 0;
        }

        _rejectCounts[key] = next;
        _changed.OnNext(key);
    }

    /// <summary>在庫の枚数を取得します。</summary>
    public virtual int GetCount(DenominationKey key) => _counts.GetValueOrDefault(NormalizeKey(key));

    /// <summary>全庫（還流・回収・リジェクト）の合計枚数を取得します。</summary>
    public virtual int GetTotalCount(DenominationKey key)
    {
        key = NormalizeKey(key);
        return _counts.GetValueOrDefault(key, 0) +
               _collectionCounts.GetValueOrDefault(key, 0) +
               _rejectCounts.GetValueOrDefault(key, 0);
    }

    /// <summary>在庫をすべてクリアします。</summary>
    public void Clear()
    {
        _counts.Clear();
        _collectionCounts.Clear();
        _rejectCounts.Clear();
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
                    if (TryParseKey(actualKey, out var denKey, out var currency) && denKey != null)
                    {
                        AddCollection(denKey, kv.Value);
                    }
                }
                else if (kv.Key.StartsWith("REJ:"))
                {
                    var actualKey = kv.Key[4..];
                    if (TryParseKey(actualKey, out var denKey, out var currency) && denKey != null)
                    {
                        AddReject(denKey, kv.Value);
                    }
                }
                else
                {
                    if (TryParseKey(kv.Key, out var denKey, out var currency) && denKey != null)
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

    private static DenominationKey NormalizeKey(DenominationKey key) =>
        string.IsNullOrEmpty(key.CurrencyCode)
            ? key with { CurrencyCode = DenominationKey.DefaultCurrencyCode }
            : key;
}
