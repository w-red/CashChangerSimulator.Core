using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace CashChangerSimulator.Core.Managers;

/// <summary>入金・出金・在庫・履歴を統括するマネージャークラス。</summary>
/// <remarks>
/// 在庫データ（Inventory）と履歴（TransactionHistory）を操作し、
/// 設定に基づいた入金の振り分けや、アルゴリズムによる出金の内訳計算（ChangeCalculator）を統合します。
/// シミュレータのドメインロジックの中核を担います。
/// </remarks>
public class CashChangerManager
{
    private readonly Inventory inventory;
    private readonly TransactionHistory history;
    private readonly ConfigurationProvider configProvider;
    private readonly ILogger<CashChangerManager> logger = LogProvider.CreateLogger<CashChangerManager>();

    /// <summary>
    /// Initializes a new instance of the <see cref="CashChangerManager"/> class.
    /// コンストラクタ（後方互換用）。
    /// </summary>
    /// <param name="inventory">在庫。</param>
    /// <param name="history">取引履歴。</param>
    public CashChangerManager(Inventory inventory, TransactionHistory history)
        : this(inventory, history, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CashChangerManager"/> class.（後方互換: calculator 引数は無視されます）.</summary>
    /// <param name="inventory">在庫。</param>
    /// <param name="history">取引履歴。</param>
    /// <param name="calculator">使用されません (互換性維持のみ)。</param>
    /// <param name="configProvider">設定プロバイダー。</param>
    [Obsolete("Use the 3-argument constructor (inventory, history, configProvider) instead.")]
    public CashChangerManager(Inventory inventory, TransactionHistory history, object? calculator, ConfigurationProvider? configProvider)
        : this(inventory, history, configProvider)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CashChangerManager"/> class.必要な依存コンポーネントを注入してマネージャーを初期化します。</summary>
    /// <remarks>指定されない場合はデフォルトの設定プロバイダーが使用されます。</remarks>
    /// <param name="inventory">在庫。</param>
    /// <param name="history">取引履歴。</param>
    /// <param name="configProvider">設定プロバイダー。</param>
    public CashChangerManager(Inventory inventory, TransactionHistory history, ConfigurationProvider? configProvider)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(history);

        this.inventory = inventory;
        this.history = history;
        this.configProvider = configProvider ?? new ConfigurationProvider();
    }

    /// <summary>入金を処理します。</summary>
    /// <remarks>金種の設定（リサイクル可否、満杯しきい値）に基づき、通常庫または回収庫に振り分けます。</remarks>
    /// <param name="counts">投入された金種ごとの枚数内訳。</param>
    public virtual void Deposit(IReadOnlyDictionary<DenominationKey, int> counts)
    {
        ArgumentNullException.ThrowIfNull(counts);
        decimal total = 0;
        foreach (var (key, count) in counts)
        {
            var setting = configProvider.Config.GetDenominationSetting(key);

            if (!setting.IsDepositable)
            {
                logger.ZLogWarning($"Denomination {key} is not depositable. Skipping.");
                continue;
            }

            if (!setting.IsRecyclable)
            {
                // 非リサイクル金種は回収庫へ
                inventory.AddCollection(key, count);
            }
            else
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

            total += key.Value * count;
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
    public virtual void Dispense(IReadOnlyDictionary<DenominationKey, int> counts)
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
    public virtual void Dispense(decimal amount, string? currencyCode = null)
    {
        var counts = ChangeCalculator.Calculate(inventory, amount, currencyCode, filter: k =>
        {
            var setting = configProvider.Config.GetDenominationSetting(k);
            return setting.IsRecyclable;
        });
        Dispense(counts);
    }

    /// <summary>リサイクル在庫をすべて回収庫へ移動します。</summary>
    public virtual void PurgeCash()
    {
        var keys = inventory.AllCounts.Select(kv => kv.Key).ToList();
        var counts = new Dictionary<DenominationKey, int>();

        foreach (var key in keys)
        {
            var count = inventory.GetCount(key);
            if (count > 0)
            {
                inventory.Add(key, -count);
                inventory.AddCollection(key, count);
                counts[key] = count;
            }
        }

        if (counts.Count > 0)
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
