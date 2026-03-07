using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using Microsoft.Extensions.Logging;
using System.Linq;
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
    private readonly Inventory _inventory;
    private readonly TransactionHistory _history;
    private readonly ChangeCalculator _calculator;
    private readonly ConfigurationProvider _configProvider;
    private readonly ILogger<CashChangerManager> _logger = LogProvider.CreateLogger<CashChangerManager>();

    /// <summary>
    /// コンストラクタ（後方互換用）。
    /// </summary>
    public CashChangerManager(Inventory inventory, TransactionHistory history, ChangeCalculator calculator)
        : this(inventory, history, calculator, null)
    {
    }

    /// <summary>必要な依存コンポーネントを注入してマネージャーを初期化します。</summary>
    /// <remarks>指定されない場合はデフォルトの設定プロバイダーが使用されます。</remarks>
    public CashChangerManager(Inventory inventory, TransactionHistory history, ChangeCalculator calculator, ConfigurationProvider? configProvider)
    {
        _inventory = inventory;
        _history = history;
        _calculator = calculator;
        _configProvider = configProvider ?? new ConfigurationProvider();
    }

    /// <summary>入金を処理します。</summary>
    /// <remarks>金種の設定（リサイクル可否、満杯しきい値）に基づき、通常庫または回収庫に振り分けます。</remarks>
    /// <param name="counts">投入された金種ごとの枚数内訳。</param>
    public virtual void Deposit(IReadOnlyDictionary<DenominationKey, int> counts)
    {
        decimal total = 0;
        foreach (var (key, count) in counts)
        {
            var setting = _configProvider.Config.GetDenominationSetting(key);

            if (!setting.IsRecyclable)
            {
                // 非リサイクル金種は回収庫へ
                _inventory.AddCollection(key, count);
            }
            else
            {
                // オーバーフロー処理
                var current = _inventory.GetCount(key);
                var canAccept = Math.Max(0, setting.Full - current);

                if (count > canAccept)
                {
                    if (canAccept > 0)
                    {
                        _inventory.Add(key, canAccept);
                    }
                    _inventory.AddCollection(key, count - canAccept);
                }
                else
                {
                    _inventory.Add(key, count);
                }
            }
            total += key.Value * count;
        }

        var currencyCode = counts.Keys.FirstOrDefault()?.CurrencyCode ?? "---";
        _logger.ZLogInformation($"Deposit: {total} {currencyCode}");

        _history.Add(new TransactionEntry(
            DateTimeOffset.Now,
            TransactionType.Deposit,
            total,
            counts
        ));
    }

    /// <summary>出金を処理する。</summary>
    /// <param name="counts">放出する金種ごとの枚数内訳。</param>
    public virtual void Dispense(IReadOnlyDictionary<DenominationKey, int> counts)
    {
        decimal total = 0;
        foreach (var (key, count) in counts)
        {
            _inventory.Add(key, -count);
            total += key.Value * count;
        }

        var currencyCode = counts.Keys.FirstOrDefault()?.CurrencyCode ?? "---";
        _logger.ZLogInformation($"Dispense: {total} {currencyCode}");

        _history.Add(new TransactionEntry(
            DateTimeOffset.Now,
            TransactionType.Dispense,
            total,
            counts
        ));
    }

    /// <summary>指定された金額を出金する。内訳は自動計算される。</summary>
    /// <param name="amount">出金する合計金額。</param>
    /// <param name="currencyCode">通貨コード。</param>
    public virtual void Dispense(decimal amount, string? currencyCode = null)
    {
        var counts = _calculator.Calculate(_inventory, amount, currencyCode, filter: k =>
        {
            var setting = _configProvider.Config.GetDenominationSetting(k);
            return setting.IsRecyclable;
        });
        Dispense(counts);
    }
    /// <summary>リサイクル在庫をすべて回収庫へ移動します。</summary>
    public virtual void PurgeCash()
    {
        var keys = _inventory.AllCounts.Select(kv => kv.Key).ToList();
        var counts = new Dictionary<DenominationKey, int>();
        
        foreach (var key in keys)
        {
            var count = _inventory.GetCount(key);
            if (count > 0)
            {
                _inventory.Add(key, -count);
                _inventory.AddCollection(key, count);
                counts[key] = count;
            }
        }

        if (counts.Count > 0)
        {
            _logger.ZLogInformation($"PurgeCash: Moved all recyclable inventory to collection.");
            _history.Add(new TransactionEntry(
                DateTimeOffset.Now,
                TransactionType.Dispense, // Purge is similar to dispense from recycling
                counts.Sum(kv => kv.Key.Value * kv.Value),
                counts
            ));
        }
    }
}
