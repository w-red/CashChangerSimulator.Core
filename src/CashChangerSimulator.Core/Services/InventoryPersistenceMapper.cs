using CashChangerSimulator.Core.Models;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace CashChangerSimulator.Core.Services;

/// <summary>Inventory クラスの永続化(シリアライズ・デシリアライズ)をサポートするマッパークラス。</summary>
public static class InventoryPersistenceMapper
{
    private static readonly ILogger Logger = LogProvider.CreateLogger<Inventory>();

    /// <summary>Inventory インスタンスをディクショナリ形式に変換します。</summary>
    /// <param name="inventory">在庫インスタンス。</param>
    /// <returns>シリアライズ用ディクショナリ。</returns>
    public static Dictionary<string, int> ToDictionary(Inventory inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        var result = new Dictionary<string, int>();

        foreach (var kv in inventory.AllCounts)
        {
            result[$"{kv.Key.CurrencyCode}{DenominationKey.KeySeparator}{kv.Key.ToDenominationString()}"] = kv.Value;
        }

        foreach (var kv in inventory.CollectionCounts)
        {
            result[$"COL{DenominationKey.KeySeparator}{kv.Key.CurrencyCode}{DenominationKey.KeySeparator}{kv.Key.ToDenominationString()}"] = kv.Value;
        }

        foreach (var kv in inventory.RejectCounts)
        {
            result[$"REJ{DenominationKey.KeySeparator}{kv.Key.CurrencyCode}{DenominationKey.KeySeparator}{kv.Key.ToDenominationString()}"] = kv.Value;
        }

        return result;
    }

    /// <summary>ディクショナリ形式のデータから Inventory インスタンスを復元します。</summary>
    /// <param name="inventory">復元先の在庫インスタンス。</param>
    /// <param name="data">ソースデータ。</param>
    public static void LoadFromDictionary(Inventory inventory, IReadOnlyDictionary<string, int> data)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(data);
        inventory.CheckDisposed();

        foreach (var kv in data)
        {
            try
            {
                if (kv.Key.StartsWith("COL:", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseKey(kv.Key[4..], out var denKey))
                    {
                        inventory.AddCollection(denKey!, kv.Value);
                    }
                }
                else if (kv.Key.StartsWith("REJ:", StringComparison.OrdinalIgnoreCase))
                {
                    if (TryParseKey(kv.Key[4..], out var denKey))
                    {
                        inventory.AddReject(denKey!, kv.Value);
                    }
                }
                else
                {
                    if (TryParseKey(kv.Key, out var denKey))
                    {
                        inventory.SetCount(denKey!, kv.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.ZLogWarning($"Failed to load inventory key: {kv.Key}. Error: {ex.Message}");
            }
        }
    }

    private static bool TryParseKey(string fullKey, out DenominationKey? key)
    {
        key = null;
        var parts = fullKey.Split(DenominationKey.KeySeparator);
        if (parts.Length == 2)
        {
            if (string.IsNullOrEmpty(parts[0]))
            {
                return false;
            }

            if (DenominationKey.TryParse(parts[1], parts[0], out var parsedKey))
            {
                key = parsedKey;
                return true;
            }
        }

        return false;
    }
}
