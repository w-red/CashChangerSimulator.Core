/* Stryker disable all */

using CashChangerSimulator.Core.Configuration;

using CashChangerSimulator.Core.Models;

using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace CashChangerSimulator.Core.Managers;

/// <summary>入金・出金・在庫・履歴を統括するマネージャークラス。</summary>
/// <remarks>
/// 在庫データ(Inventory)と履歴(TransactionHistory)を操作し、
/// 設定に基づいた入金の振り分けや、アルゴリズムによる出金の内訳計算(ChangeCalculator)を統合します。
/// シミュレータのドメインロジックの中核を担います。
/// </remarks>
/// <param name="inventory">在庫。</param>
/// <param name="history">取引履歴。</param>
/// <param name="configProvider">設定プロバイダー。</param>
public class CashChangerManager(
    Inventory inventory,
    TransactionHistory history,
    ConfigurationProvider? configProvider)
{
    private readonly Inventory inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
    private readonly TransactionHistory history = history ?? throw new ArgumentNullException(nameof(history));
    private readonly ConfigurationProvider configProvider = configProvider ?? new ConfigurationProvider();
    private readonly ILogger<CashChangerManager> logger = LogProvider.CreateLogger<CashChangerManager>();


    /// <summary>入金を処理します。</summary>
    /// <remarks>金種の設定(リサイクル可否、満杯しきい値)に基づき、通常庫または回収庫に振り分けます。</remarks>
    /// <param name="counts">投入された金種ごとの枚数内訳。</param>
    public virtual void Deposit(
        IReadOnlyDictionary<DenominationKey, int> counts)
    {
        ArgumentNullException.ThrowIfNull(counts);
        decimal total = 0;
        foreach (var (key, count) in counts)
        {
            if (ProcessDepositItem(key, count))
            {
                total += key.Value * count;
            }
        }

        var currencyCode = counts.Keys.FirstOrDefault()?.CurrencyCode ?? "---";
        logger.ZLogInformation($"Deposit: {total} {currencyCode}");

        history.Add(new TransactionEntry(
            DateTimeOffset.Now,
            TransactionType.Deposit,
            total,
            counts));
    }

    /// <summary>出金を処理する。</summary>
    /// <param name="counts">放出する金種ごとの枚数内訳。</param>
    public virtual void Dispense(
        IReadOnlyDictionary<DenominationKey, int> counts)
    {
        ArgumentNullException.ThrowIfNull(counts);
        decimal total = 0;
        foreach (var (key, count) in counts)
        {
            inventory.Add(key, -count);
            total += key.Value * count;
        }

        var currencyCode = counts.Keys.FirstOrDefault()?.CurrencyCode ?? "---";
        logger.ZLogInformation($"Dispense: {total} {currencyCode}");

        history.Add(new TransactionEntry(
            DateTimeOffset.Now,
            TransactionType.Dispense,
            total,
            counts));
    }

    /// <summary>指定された金額を出金する。内訳は自動計算される。</summary>
    /// <param name="amount">出金する合計金額。</param>
    /// <param name="currencyCode">通貨コード。</param>
    public virtual void Dispense(
        decimal amount,
        string? currencyCode = null)
    {
        var counts = ChangeCalculator.Calculate(inventory, amount, currencyCode, filter: k =>
        {
            var setting = configProvider.Config.GetDenominationSetting(k);
            return setting.IsRecyclable;
        });
        Dispense(counts);
    }

    /// <summary>すべてのリサイクル庫を回収庫に移動します。</summary>
    /// <returns>回収された金種と枚数の内訳。</returns>
    public virtual IReadOnlyDictionary<DenominationKey, int> PurgeCash()
    {
        var counts = inventory.AllCounts
            .Where(kv => configProvider.Config.GetDenominationSetting(kv.Key).IsRecyclable)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        CollectRecyclableInventory(counts);

        RecordPurgeHistory(counts);

        return counts;
    }

    /// <summary>在庫枚数を直接調整します(管理用)。</summary>
    /// <param name="counts">調整する金種と枚数のディクショナリ。</param>
    public virtual void Adjust(
        IReadOnlyDictionary<DenominationKey, int> counts)
    {
        ArgumentNullException.ThrowIfNull(counts);
        foreach (var (key, count) in counts)
        {
            inventory.SetCount(key, count);
        }

        logger.ZLogInformation($"Adjust: Inventory adjusted manually for {counts.Count} denominations.");
    }

    private bool ProcessDepositItem(
        DenominationKey key,
        int count)
    {
        var setting = configProvider.Config.GetDenominationSetting(key);

        if (!setting.IsDepositable)
        {
            logger.ZLogWarning($"Denomination {key} is not depositable. Skipping.");
            return false;
        }

        if (!setting.IsRecyclable)
        {
            // 非リサイクル金種は回収庫へ
            inventory.AddCollection(key, count);
        }
        else
        {
            HandleRecyclableDeposit(key, count, setting);
        }

        return true;
    }

    private void HandleRecyclableDeposit(
        DenominationKey key,
        int count,
        DenominationSettings setting)
    {
        // オーバーフロー処理
        var current = inventory.GetCount(key);
        var canAccept = Math.Max(0, setting.Full - current);

        if (count > canAccept)
        {
            if (canAccept > 0)
            {
                inventory.Add(key, canAccept);
            }

            inventory.AddCollection(key, count - canAccept);
        }
        else
        {
            inventory.Add(key, count);
        }
    }

    private void CollectRecyclableInventory(
        Dictionary<DenominationKey, int> counts)
    {
        foreach (var kv in counts)
        {
            if (kv.Value > 0)
            {
                inventory.Add(kv.Key, -kv.Value);
                inventory.AddCollection(kv.Key, kv.Value);
            }
        }
    }

    private void RecordPurgeHistory(
        Dictionary<DenominationKey, int> counts)
    {
        var sum = counts.Sum(kv => kv.Value);
        if (sum > 0)
        {
            logger.ZLogInformation($"PurgeCash: Moved all recyclable inventory to collection.");
            history.Add(new TransactionEntry(
                DateTimeOffset.Now,
                TransactionType.Dispense, // Purge is similar to dispense from recycling
                counts.Sum(kv => kv.Key.Value * kv.Value),
                counts));
        }
    }
}
