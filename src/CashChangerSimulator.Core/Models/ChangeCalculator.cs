using System;
using System.Collections.Generic;
using System.Linq;
using CashChangerSimulator.Core.Exceptions;

namespace CashChangerSimulator.Core.Models;

/// <summary>
/// お釣りの金種組み合わせを計算するクラス。
/// </summary>
public class ChangeCalculator
{
    /// <summary>指定された在庫から、指定された金額を支払うための金種内訳を計算する。</summary>
    /// <param name="inventory">現在の在庫。</param>
    /// <param name="targetAmount">支払いたい合計金額。</param>
    /// <returns>金種キーと枚数のディクショナリ。</returns>
    /// <exception cref="InsufficientCashException">在庫不足や端数不一致により計算できない場合。</exception>
    public IReadOnlyDictionary<DenominationKey, int> Calculate(IReadOnlyInventory inventory, decimal targetAmount)
    {
        var result = new Dictionary<DenominationKey, int>();
        decimal remaining = targetAmount;

        // 在庫のある金種を大きい順に取得
        // 同じ額面の場合は、一旦紙幣を優先する（CashType.Bill > Coin）
        var availableKeys = GetAvailableDenominationKeys(inventory)
            .OrderByDescending(k => k.Value)
            .ThenByDescending(k => k.Type);

        foreach (var key in availableKeys)
        {
            if (remaining <= 0) break;

            int needed = (int)(remaining / key.Value);
            if (needed <= 0) continue;

            int available = inventory.GetCount(key);
            int countToTake = Math.Min(needed, available);

            if (countToTake > 0)
            {
                result[key] = countToTake;
                remaining -= key.Value * countToTake;
            }
        }

        if (remaining > 0)
        {
            throw new InsufficientCashException($"要求された金額 {targetAmount} を支払うための在庫が不足しているか、端数が合いません（残り: {remaining}）。");
        }

        return result;
    }

    private IEnumerable<DenominationKey> GetAvailableDenominationKeys(IReadOnlyInventory inventory)
    {
        // 実際には IReadOnlyInventory の実装（Inventory クラス）から全金種キーを取得する
        if (inventory is Inventory inv)
        {
            return inv.AllCounts.Select(kv => kv.Key);
        }
        return Enumerable.Empty<DenominationKey>();
    }
}
