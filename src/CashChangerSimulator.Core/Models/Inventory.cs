/* Stryker disable all */
using System.Diagnostics.CodeAnalysis;

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

    private readonly CashCassette recyclableCassette = new();
    private readonly CashCassette collectionCassette = new();
    private readonly CashCassette rejectCassette = new();
    private readonly CashCassette escrowCassette = new();

    private readonly CompositeDisposable disposables = [];
    private readonly Lock @lock = new();

    private bool disposed;
    private bool isForcedDiscrepancy;

    /// <inheritdoc/>
    [SuppressMessage("Microsoft.Reliability", "CA2000:DisposeObjectsBeforeLosingScope", Justification = "AddTo(disposables) ensures proper disposal.")]
    protected Inventory()
    {
        var subject = new Subject<DenominationKey>();
        disposables.Add(subject);
        Changed = subject;
    }

    /// <inheritdoc/>
    public virtual Observable<DenominationKey> Changed { get; }

    /// <summary>在庫の不一致が発生しているかどうかを取得または設定します。</summary>
    /// <remarks>通常、回収庫またはリジェクト庫に現金がある場合に不一致と見なされます。手動での設定も可能です。</remarks>
    public virtual bool HasDiscrepancy
    {
        get
        {
            lock (@lock)
            {
                return isForcedDiscrepancy || collectionCassette.HasDiscrepancy() || rejectCassette.HasDiscrepancy();
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
    public virtual IEnumerable<KeyValuePair<DenominationKey, int>> AllCounts => recyclableCassette.GetAll();

    /// <summary>回収庫（オーバーフロー等）の全枚数。</summary>
    public virtual IEnumerable<KeyValuePair<DenominationKey, int>> CollectionCounts => collectionCassette.GetAll();

    /// <summary>リジェクト庫（汚損等）の全枚数。</summary>
    public virtual IEnumerable<KeyValuePair<DenominationKey, int>> RejectCounts => rejectCassette.GetAll();

    /// <summary>入金トレイ（エスクロー）の全枚数。</summary>
    public virtual IEnumerable<KeyValuePair<DenominationKey, int>> EscrowCounts => escrowCassette.GetAll();

    /// <summary>在庫管理インスタンスを生成・初期化します。</summary>
    /// <returns>初期化済みの <see cref="Inventory"/> インスタンス。</returns>
    public static Inventory Create()
    {
        return new Inventory();
    }

    /// <summary>指定された金種の枚数を追加します。</summary>
    /// <param name="key">金種キー。</param>
    /// <param name="count">追加する枚数（負の値も可）。</param>
    public virtual void Add(DenominationKey key, int count)
    {
        UpdateBucket(recyclableCassette, key, count, "Inventory.Add");
    }

    /// <summary>指定された金種の枚数を上書き設定します。</summary>
    /// <param name="key">金種キー。</param>
    /// <param name="count">設定する枚数。</param>
    public virtual void SetCount(DenominationKey key, int count)
    {
        ArgumentNullException.ThrowIfNull(key);
        key = NormalizeKey(key);
        if (count < 0)
        {
            logger.ZLogWarning($"Inventory.SetCount: Ignoring negative count {count} for {key}");
            return;
        }

        recyclableCassette.SetCount(key, count);
        ((Subject<DenominationKey>)Changed).OnNext(key);
    }

    /// <summary>指定された金種の枚数を回収庫に追加する。</summary>
    /// <param name="key">金種キー。</param>
    /// <param name="count">追加する枚数。</param>
    public virtual void AddCollection(DenominationKey key, int count)
    {
        UpdateBucket(collectionCassette, key, count, "Inventory.AddCollection");
    }

    /// <summary>指定された金種の枚数をリジェクト庫に追加する。</summary>
    /// <param name="key">金種キー。</param>
    /// <param name="count">追加する枚数。</param>
    public virtual void AddReject(DenominationKey key, int count)
    {
        UpdateBucket(rejectCassette, key, count, "Inventory.AddReject");
    }

    /// <summary>指定された金種の枚数を入金トレイ（エスクロー）に追加する。</summary>
    /// <param name="key">金種キー。</param>
    /// <param name="count">追加する枚数。</param>
    public virtual void AddEscrow(DenominationKey key, int count)
    {
        UpdateBucket(escrowCassette, key, count, "Inventory.AddEscrow");
    }

    /// <summary>入金トレイ（エスクロー）をクリアします。</summary>
    public virtual void ClearEscrow()
    {
        var keys = escrowCassette.Clear();

        foreach (var key in keys)
        {
            ((Subject<DenominationKey>)Changed).OnNext(key);
        }

        logger.ZLogDebug($"Inventory.ClearEscrow finished.");
    }

    /// <summary>在庫の枚数を取得します。</summary>
    /// <param name="key">金種キー。</param>
    /// <returns>在庫枚数。</returns>
    public virtual int GetCount(DenominationKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return recyclableCassette.GetCount(NormalizeKey(key));
    }

    /// <summary>全庫（還流・回収・リジェクト）の合計枚数を取得します。</summary>
    /// <param name="key">金種キー。</param>
    /// <returns>合計枚数。</returns>
    public virtual int GetTotalCount(DenominationKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        key = NormalizeKey(key);

        return recyclableCassette.GetCount(key) +
               collectionCassette.GetCount(key) +
               rejectCassette.GetCount(key) +
               escrowCassette.GetCount(key);
    }

    /// <summary>在庫をすべてクリアします。</summary>
    public void Clear()
    {
        recyclableCassette.Clear();
        collectionCassette.Clear();
        rejectCassette.Clear();
        escrowCassette.Clear();
    }

    /// <summary>現在の在庫の合計金額を計算します。</summary>
    /// <param name="currencyCode">フィルタリングする通貨コード（任意）。</param>
    /// <returns>合計金額。</returns>
    public virtual decimal CalculateTotal(string? currencyCode = null)
    {
        return recyclableCassette.CalculateTotal(currencyCode) +
               collectionCassette.CalculateTotal(currencyCode) +
               rejectCassette.CalculateTotal(currencyCode) +
               escrowCassette.CalculateTotal(currencyCode);
    }

    /// <summary>現在の在庫を保存用のデータ形式に変換します。</summary>
    /// <returns>保存用ディクショナリ。</returns>
    public Dictionary<string, int> ToDictionary()
    {
        var result = new Dictionary<string, int>();
        recyclableCassette.AddToDictionary(result, string.Empty);
        collectionCassette.AddToDictionary(result, "COL");
        rejectCassette.AddToDictionary(result, "REJ");
        return result;
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
                    if (TryParseKey(kv.Key[4..], out var denKey) && denKey != null)
                    {
                        AddCollection(denKey, kv.Value);
                    }
                }
                else if (kv.Key.StartsWith("REJ:", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseKey(kv.Key[4..], out var denKey) && denKey != null)
                    {
                        AddReject(denKey, kv.Value);
                    }
                }
                else
                {
                    if (TryParseKey(kv.Key, out var denKey) && denKey != null)
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
            disposables.Dispose();
        }

        disposed = true;
    }

    private static bool TryParseKey(string fullKey, out DenominationKey? key)
    {
        key = null;
        var parts = fullKey.Split(DenominationKey.KeySeparator);
        if (parts.Length == 2)
        {
            if (DenominationKey.TryParse(parts[1], parts[0], out var parsedKey) && parsedKey != null)
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

    private void UpdateBucket(CashCassette cassette, DenominationKey key, int count, string methodName)
    {
        ArgumentNullException.ThrowIfNull(key);
        key = NormalizeKey(key);

        if (cassette.UpdateCount(key, count, logger, methodName))
        {
            ((Subject<DenominationKey>)Changed).OnNext(key);
            logger.ZLogDebug($"{methodName} finished. New Total: {CalculateTotal()}");
        }
    }

    /// <summary>論理的な金庫（カセット）を表現する内部クラスです。</summary>
    private sealed class CashCassette
    {
        private readonly Dictionary<DenominationKey, int> counts = [];
        private readonly Lock @lock = new();

        public IEnumerable<KeyValuePair<DenominationKey, int>> GetAll()
        {
            lock (@lock)
            {
                return [.. counts];
            }
        }

        public int GetCount(DenominationKey key)
        {
            lock (@lock)
            {
                return counts.GetValueOrDefault(key, 0);
            }
        }

        public void SetCount(DenominationKey key, int count)
        {
            lock (@lock)
            {
                counts[key] = count;
            }
        }

        public bool UpdateCount(DenominationKey key, int count, ILogger logger, string methodName)
        {
            if (count == 0)
            {
                return false;
            }

            lock (@lock)
            {
                var current = counts.GetValueOrDefault(key, 0);
                var next = Math.Max(0, current + count);
                if (current + count < 0)
                {
                    logger.ZLogWarning($"{methodName}: Resulting count for {key} is negative ({current + count}). Setting to 0.");
                }

                counts[key] = next;
                return true;
            }
        }

        public List<DenominationKey> Clear()
        {
            List<DenominationKey> keys;
            lock (@lock)
            {
                keys = [.. counts.Keys];
                counts.Clear();
            }

            return keys;
        }

        public decimal CalculateTotal(string? currencyCode)
        {
            lock (@lock)
            {
                return counts
                    .Where(kv => currencyCode == null || kv.Key.CurrencyCode == currencyCode)
                    .Sum(kv => kv.Key.Value * kv.Value);
            }
        }

        public void AddToDictionary(Dictionary<string, int> result, string prefix)
        {
            lock (@lock)
            {
                foreach (var kv in counts)
                {
                    var formattedPrefix = string.IsNullOrEmpty(prefix) ? string.Empty : $"{prefix}:";
                    result[$"{formattedPrefix}{kv.Key.CurrencyCode}{DenominationKey.KeySeparator}{kv.Key.PrefixChar}{kv.Key.Value}"] = kv.Value;
                }
            }
        }

        public bool HasDiscrepancy()
        {
            lock (@lock)
            {
                return counts.Values.Any(v => v > 0);
            }
        }
    }
}
