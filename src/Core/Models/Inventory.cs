using System.Threading;
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
public class Inventory : IReadOnlyInventory, IDisposable
{
    private readonly ILogger<Inventory> logger = LogProvider.CreateLogger<Inventory>();
    private readonly Dictionary<DenominationKey, int> counts = [];
    private readonly Dictionary<DenominationKey, int> collectionCounts = [];
    private readonly Dictionary<DenominationKey, int> rejectCounts = [];
    private readonly Dictionary<DenominationKey, int> escrowCounts = [];
    private readonly Subject<DenominationKey> changed = new();
    private readonly Lock @lock = new();

    private bool disposed;
    private bool isForcedDiscrepancy;

    /// <inheritdoc/>
    public virtual Observable<DenominationKey> Changed => changed;

    /// <summary>在庫の不一致が発生しているかどうかを取得または設定します。</summary>
    /// <remarks>通常、回収庫またはリジェクト庫に現金がある場合に不一致と見なされます。手動での設定も可能です。</remarks>
    public virtual bool HasDiscrepancy
    {
        get
        {
            lock (@lock)
            {
                return isForcedDiscrepancy || collectionCounts.Values.Any(v => v > 0) || rejectCounts.Values.Any(v => v > 0);
            }
        }
        set
        {
            lock (@lock)
            {
                isForcedDiscrepancy = value;
            }
        }
    }

    /// <summary>通常庫（リサイクル可能）の全枚数。</summary>
    public virtual IEnumerable<KeyValuePair<DenominationKey, int>> AllCounts
    {
        get
        {
            lock (@lock)
            {
                return [.. counts];
            }
        }
    }

    /// <summary>回収庫（オーバーフロー等）の全枚数。</summary>
    public virtual IEnumerable<KeyValuePair<DenominationKey, int>> CollectionCounts
    {
        get
        {
            lock (@lock)
            {
                return [.. collectionCounts];
            }
        }
    }

    /// <summary>リジェクト庫（汚損等）の全枚数。</summary>
    public virtual IEnumerable<KeyValuePair<DenominationKey, int>> RejectCounts
    {
        get
        {
            lock (@lock)
            {
                return [.. rejectCounts];
            }
        }
    }

    /// <summary>入金トレイ（エスクロー）の全枚数。</summary>
    public virtual IEnumerable<KeyValuePair<DenominationKey, int>> EscrowCounts
    {
        get
        {
            lock (@lock)
            {
                return [.. escrowCounts];
            }
        }
    }

    /// <summary>指定された金種の枚数を追加します。</summary>
    /// <param name="key">金種キー。</param>
    /// <param name="count">追加する枚数（負の値も可）。</param>
    /// <remarks>
    /// 金種キーを正規化し、指定された枚数を通常庫に加算します。
    /// 正負両方の値を許容しますが、最終的な在庫数が負にならないよう内部で正規化されます。
    /// </remarks>
    public virtual void Add(DenominationKey key, int count)
    {
        UpdateBucket(counts, key, count, "Inventory.Add");
    }

    /// <summary>指定された金種の枚数を上書き設定します。</summary>
    /// <param name="key">金種キー。</param>
    /// <param name="count">設定する枚数。</param>
    /// <remarks>
    /// 既存の値を破棄し、指定された枚数で通常庫を更新します。
    /// 不の枚数が指定された場合は 0 として扱われます。
    /// </remarks>
    public virtual void SetCount(DenominationKey key, int count)
    {
        ArgumentNullException.ThrowIfNull(key);
        key = NormalizeKey(key);
        if (count < 0)
        {
            logger.ZLogWarning($"Inventory.SetCount: Ignoring negative count {count} for {key}");
            return;
        }

        lock (@lock)
        {
            counts[key] = count;
        }

        changed.OnNext(key);
    }

    /// <summary>指定された金種の枚数を回収庫に追加する。</summary>
    /// <param name="key">金種キー。</param>
    /// <param name="count">追加する枚数。</param>
    public virtual void AddCollection(DenominationKey key, int count)
    {
        UpdateBucket(collectionCounts, key, count, "Inventory.AddCollection");
    }

    /// <summary>指定された金種の枚数をリジェクト庫に追加する。</summary>
    /// <param name="key">金種キー。</param>
    /// <param name="count">追加する枚数。</param>
    public virtual void AddReject(DenominationKey key, int count)
    {
        UpdateBucket(rejectCounts, key, count, "Inventory.AddReject");
    }

    /// <summary>指定された金種の枚数を入金トレイ（エスクロー）に追加する。</summary>
    /// <param name="key">金種キー。</param>
    /// <param name="count">追加する枚数。</param>
    public virtual void AddEscrow(DenominationKey key, int count)
    {
        UpdateBucket(escrowCounts, key, count, "Inventory.AddEscrow");
    }

    /// <summary>入金トレイ（エスクロー）をクリアします。</summary>
    public virtual void ClearEscrow()
    {
        List<DenominationKey> keys;
        lock (@lock)
        {
            keys = escrowCounts.Keys.ToList();
            escrowCounts.Clear();
        }

        foreach (var key in keys)
        {
            changed.OnNext(key);
        }

        logger.ZLogDebug($"Inventory.ClearEscrow finished.");
    }

    /// <summary>在庫の枚数を取得します。</summary>
    /// <param name="key">金種キー。</param>
    /// <returns>在庫枚数。</returns>
    public virtual int GetCount(DenominationKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (@lock)
        {
            return counts.GetValueOrDefault(NormalizeKey(key));
        }
    }

    /// <summary>全庫（還流・回収・リジェクト）の合計枚数を取得します。</summary>
    /// <param name="key">金種キー。</param>
    /// <returns>合計枚数。</returns>
    public virtual int GetTotalCount(DenominationKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        key = NormalizeKey(key);
        lock (@lock)
        {
            return counts.GetValueOrDefault(key, 0) +
                   collectionCounts.GetValueOrDefault(key, 0) +
                   rejectCounts.GetValueOrDefault(key, 0) +
                   escrowCounts.GetValueOrDefault(key, 0);
        }
    }

    /// <summary>在庫をすべてクリアします。</summary>
    public void Clear()
    {
        lock (@lock)
        {
            counts.Clear();
            collectionCounts.Clear();
            rejectCounts.Clear();
            escrowCounts.Clear();
        }
    }

    /// <summary>現在の在庫の合計金額を計算します。</summary>
    /// <param name="currencyCode">フィルタリングする通貨コード（任意）。</param>
    /// <remarks>通常庫、回収庫、リジェクト庫のすべての合計を計算します。</remarks>
    /// <returns>合計金額。</returns>
    public virtual decimal CalculateTotal(string? currencyCode = null)
    {
        lock (@lock)
        {
            return new[] { counts, collectionCounts, rejectCounts, escrowCounts }
                .SelectMany(d => d)
                .Where(kv => currencyCode == null || kv.Key.CurrencyCode == currencyCode)
                .Sum(kv => kv.Key.Value * kv.Value);
        }
    }

    /// <summary>現在の在庫を保存用のデータ形式に変換します。</summary>
    /// <returns>保存用ディクショナリ。</returns>
    public Dictionary<string, int> ToDictionary()
    {
        lock (@lock)
        {
            var result = counts.ToDictionary(
                kv => $"{kv.Key.CurrencyCode}{DenominationKey.KeySeparator}{kv.Key.PrefixChar}{kv.Key.Value}",
                kv => kv.Value);
            foreach (var kv in collectionCounts)
            {
                result[$"COL:{kv.Key.CurrencyCode}{DenominationKey.KeySeparator}{kv.Key.PrefixChar}{kv.Key.Value}"] = kv.Value;
            }

            foreach (var kv in rejectCounts)
            {
                result[$"REJ:{kv.Key.CurrencyCode}{DenominationKey.KeySeparator}{kv.Key.PrefixChar}{kv.Key.Value}"] = kv.Value;
            }

            return result;
        }
    }

    /// <summary>文字列キーのディクショナリから在庫を復元します。</summary>
    /// <param name="data">復元元データ。</param>
    public void LoadFromDictionary(IReadOnlyDictionary<string, int> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        foreach (var kv in data)
        {
            try
            {
                if (kv.Key.StartsWith("COL:", StringComparison.OrdinalIgnoreCase))
                {
                    var actualKey = kv.Key[4..];
                    if (TryParseKey(actualKey, out var denKey, out var _) && denKey != null)
                    {
                        AddCollection(denKey, kv.Value);
                    }
                }
                else if (kv.Key.StartsWith("REJ:", StringComparison.OrdinalIgnoreCase))
                {
                    var actualKey = kv.Key[4..];
                    if (TryParseKey(actualKey, out var denKey, out var _) && denKey != null)
                    {
                        AddReject(denKey, kv.Value);
                    }
                }
                else
                {
                    if (TryParseKey(kv.Key, out var denKey, out var _) && denKey != null)
                    {
                        SetCount(denKey, kv.Value);
                    }
                }
            }
            catch (ArgumentException ex)
            {
                logger.ZLogWarning($"Failed to load inventory key (ArgumentException): {kv.Key}. Error: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                logger.ZLogWarning($"Failed to load inventory key (InvalidOperationException): {kv.Key}. Error: {ex.Message}");
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>リソースを解放します。</summary>
    /// <param name="disposing">明示的な破棄かどうか。</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        if (disposing)
        {
            changed.Dispose();
        }

        disposed = true;
    }

    private static bool TryParseKey(string fullKey, out DenominationKey? key, out string? currencyCode)
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
        (key.CurrencyCode == null || string.IsNullOrEmpty(key.CurrencyCode))
            ? key with { CurrencyCode = DenominationKey.DefaultCurrencyCode }
            : key;

    private void UpdateBucket(Dictionary<DenominationKey, int> bucket, DenominationKey key, int count, string methodName)
    {
        ArgumentNullException.ThrowIfNull(key);
        key = NormalizeKey(key);
        if (count == 0)
        {
            return;
        }

        int next;
        lock (@lock)
        {
            var current = bucket.GetValueOrDefault(key, 0);
            next = Math.Max(0, current + count);
            if (current + count < 0)
            {
                logger.ZLogWarning($"{methodName}: Resulting count for {key} is negative ({current + count}). Setting to 0.");
            }

            bucket[key] = next;
        }

        changed.OnNext(key);
        logger.ZLogDebug($"{methodName} finished. New Total: {CalculateTotal()}");
    }
}
