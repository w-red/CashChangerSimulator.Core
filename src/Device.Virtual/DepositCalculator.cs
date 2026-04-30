using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace CashChangerSimulator.Device.Virtual;

/// <summary>入金終了時の計算および在庫操作を担当するクラス。</summary>
internal sealed class DepositCalculator(
    ILogger? logger,
    Inventory inventory,
    CashChangerManager? manager)
{
    private readonly ILogger? logger = logger;
    private readonly Inventory inventory = inventory;
    private readonly CashChangerManager? manager = manager;

    /// <summary>エスクロー内の現金を返却（Repay）処理します。</summary>
    public void ProcessRepay()
    {
        /* Stryker disable all */
        logger?.ZLogInformation($"Deposit Repay: Returning cash from escrow.");
        /* Stryker restore all */

        inventory.ClearEscrow();
    }

    /// <summary>要求金額に応じた釣銭（Change）計算と収納処理を行います。</summary>
    /// <param name="depositAmount">投入合計金額。</param>
    /// <param name="requiredAmount">要求金額。</param>
    /// <param name="depositCounts">投入金種の内訳。</param>
    public void ProcessChange(
        decimal depositAmount,
        decimal requiredAmount,
        IReadOnlyDictionary<DenominationKey, int> depositCounts)
    {
        decimal changeAmount = Math.Max(0, depositAmount - requiredAmount);
        var storeCounts = new Dictionary<DenominationKey, int>(depositCounts);

        /* Stryker disable all : Trace logging is non-functional */
        logger?.ZLogTrace($"EndDepositAsync: {depositAmount} - {requiredAmount} = {changeAmount}");
        /* Stryker restore all */

        if (changeAmount > 0)
        {
            var availableInEscrow = inventory.EscrowCounts.OrderByDescending(kv => kv.Key.Value).ToList();
            decimal remainingChange = changeAmount;
            foreach (var (key, countInEscrow) in availableInEscrow)
            {
                if (remainingChange <= 0)
                {
                    break;
                }

                int useCount = (int)Math.Min(countInEscrow, Math.Floor(remainingChange / key.Value));

                if (useCount > 0)
                {
                    storeCounts[key] -= useCount;
                    remainingChange -= key.Value * useCount;
                }
            }

            // 1. まずエスクローをクリアし、在庫を更新する。
            inventory.ClearEscrow();
            UpdateInventoryAndManager(storeCounts);

            // 2. その後で釣銭が必要な分だけ払い出す。
            if (remainingChange > 0 && manager != null)
            {
                manager.Dispense(remainingChange);
            }
        }
        else
        {
            inventory.ClearEscrow();
            UpdateInventoryAndManager(storeCounts);
        }
    }

    /// <summary>釣銭なしで全額を収納（NoChange）処理します。</summary>
    /// <param name="depositCounts">投入金種の内訳。</param>
    public void ProcessNoChange(
        IReadOnlyDictionary<DenominationKey, int> depositCounts)
    {
        /* Stryker disable all */
        logger?.ZLogInformation($"Deposit NoChange: Storing all cash into inventory.");
        /* Stryker restore all */

        var storeCounts = new Dictionary<DenominationKey, int>(depositCounts);
        inventory.ClearEscrow();

        UpdateInventoryAndManager(storeCounts);
    }

    private void UpdateInventoryAndManager(
        Dictionary<DenominationKey, int> storeCounts)
    {
        if (manager != null)
        {
            manager.Deposit(new Dictionary<DenominationKey, int>(storeCounts));
        }
        else
        {
            foreach (var kv in storeCounts)
            {
                if (kv.Value > 0)
                {
                    inventory.Add(kv.Key, kv.Value);
                }
            }
        }
    }
}
