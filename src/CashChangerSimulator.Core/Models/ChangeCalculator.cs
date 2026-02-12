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
    /// <summary>
    /// 指定された在庫から、指定された金額を支払うための金種内訳を計算する。
    /// </summary>
    /// <param name="inventory">現在の在庫。</param>
    /// <param name="targetAmount">支払いたい合計金額。</param>
    /// <returns>額面と枚数のディクショナリ。</returns>
    /// <exception cref="InsufficientCashException">在庫不足や端数不一致により計算できない場合。</exception>
    public IReadOnlyDictionary<int, int> Calculate(IReadOnlyInventory inventory, decimal targetAmount)
    {
        var result = new Dictionary<int, int>();
        decimal remaining = targetAmount;

        // 在庫のある金種を大きい順に取得
        // ※ 本来は「利用可能な金種マスター」を持つべきだが、一旦現在の在庫に含まれる金種から計算
        var availableDenominations = GetAvailableDenominations(inventory)
            .OrderByDescending(d => d);

        foreach (var denomination in availableDenominations)
        {
            if (remaining <= 0) break;

            int needed = (int)(remaining / denomination);
            if (needed <= 0) continue;

            int available = inventory.GetCount(denomination);
            int countToTake = Math.Min(needed, available);

            if (countToTake > 0)
            {
                result[denomination] = countToTake;
                remaining -= (decimal)denomination * countToTake;
            }
        }

        if (remaining > 0)
        {
            throw new InsufficientCashException($"要求された金額 {targetAmount} を支払うための在庫が不足しているか、端数が合いません（残り: {remaining}）。");
        }

        return result;
    }

    private IEnumerable<int> GetAvailableDenominations(IReadOnlyInventory inventory)
    {
        // Inventory の実装詳細に依存せず、一般的に使われる日本円の金種などを想定しても良いが、
        // 現状は Inventory に登録されている（枚数が0より大きい）金種を対象とする。
        // ※ 改善案: IReadOnlyInventory に「登録されている全金種リスト」を返すメソッドを追加する。
        
        // 現状の実装：1円〜10000円の標準的な金種を候補とする
        return new[] { 10000, 5000, 2000, 1000, 500, 100, 50, 10, 5, 1 };
    }
}
