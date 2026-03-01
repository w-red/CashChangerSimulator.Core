using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace CashChangerSimulator.Core.Managers;

/// <summary>在庫管理と履歴管理を統合し、実務的な入出金操作を提供するマネージャークラス。</summary>
/// <param name="inventory">在庫管理オブジェクト。</param>
/// <param name="history">履歴管理オブジェクト。</param>
/// <param name="calculator">釣銭計算オブジェクト。</param>
/// <summary>入金・出金・在庫・履歴を調整するマネージャークラス。</summary>
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

    /// <summary>
    /// コンストラクタ。
    /// </summary>
    public CashChangerManager(Inventory inventory, TransactionHistory history, ChangeCalculator calculator, ConfigurationProvider? configProvider)
    {
        _inventory = inventory;
        _history = history;
        _calculator = calculator;
        _configProvider = configProvider ?? new ConfigurationProvider();
    }

    /// <summary>入金を処理する。</summary>
    /// <param name="counts">投入された金種ごとの枚数内訳。</param>
    public virtual void Deposit(IReadOnlyDictionary<DenominationKey, int> counts)
    {
        decimal total = 0;
        foreach (var (key, count) in counts)
        {
            _inventory.Add(key, count);
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
            return setting?.IsRecyclable ?? true;
        });
        Dispense(counts);
    }
}
