using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Models;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace CashChangerSimulator.Core.Services;

/// <summary>お釣りの金種組み合わせを計算するクラス。</summary>
/// <remarks>
/// 指定された在庫情報と金額から、最適な払い出し内訳(Greedy アルゴリズム)を算出します。
/// 在庫不足や端数の不一致が発生した場合には例外(InsufficientCashException)をスローします。
/// </remarks>
public static class ChangeCalculator
{
    private static readonly ILogger Logger = LogProvider.CreateLogger<Inventory>();

    /// <summary>指定された在庫から、支払額に応じた金種の組み合わせを算出します。</summary>
    /// <param name="inventory">現在の在庫。</param>
    /// <param name="targetAmount">支払いたい合計金額。</param>
    /// <param name="currencyCode">フィルタリングする通貨コード(任意)。</param>
    /// <param name="filter">追加の金種フィルタ。</param>
    /// <returns>金種キーと枚数のディクショナリ。</returns>
    /// <exception cref="InsufficientCashException">在庫不足や端数不一致により計算できない場合。</exception>
    public static IReadOnlyDictionary<DenominationKey, int> Calculate(IReadOnlyInventory inventory, decimal targetAmount, string? currencyCode = null, Func<DenominationKey, bool>? filter = null)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        var result = new Dictionary<DenominationKey, int>();
        decimal remaining = targetAmount;

        var availableKeys = GetAvailableDenominationKeys(inventory)
            .Where(k => currencyCode == null || k.CurrencyCode == currencyCode)
            .Where(k => filter == null || filter(k))
            .OrderByDescending(k => k.Value)
            .ThenByDescending(k => k.Type);

        foreach (var key in availableKeys)
        {
            if (remaining <= 0)
            {
                break;
            }

            remaining = SelectDenominations(inventory, key, remaining, result);
        }

        if (remaining > 0)
        {
            Logger.ZLogWarning($"Insufficient cash: requested {targetAmount}, remaining {remaining}."); // Stryker disable once all
            throw new InsufficientCashException($"要求された金額 {targetAmount} を支払うための在庫が不足しているか、端数が合いません(残り: {remaining})。");
        }

        return result;
    }

    private static decimal SelectDenominations(IReadOnlyInventory inventory, DenominationKey key, decimal remaining, Dictionary<DenominationKey, int> result)
    {
        int needed = (int)(remaining / key.Value);
        if (needed <= 0)
        {
            return remaining;
        }

        int available = inventory.GetCount(key);
        int countToTake = Math.Min(needed, available);

        if (countToTake > 0)
        {
            result[key] = countToTake;
            return remaining - (key.Value * countToTake);
        }

        return remaining;
    }

    private static IEnumerable<DenominationKey> GetAvailableDenominationKeys(IReadOnlyInventory inventory)
    {
        // 実際には IReadOnlyInventory の実装(Inventory クラス)から全金種キーを取得する
        return inventory is Inventory inv ? inv.AllCounts.Select(kv => kv.Key) : [];
    }
}
