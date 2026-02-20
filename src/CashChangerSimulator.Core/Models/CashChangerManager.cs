using Microsoft.Extensions.Logging;
using ZLogger;

namespace CashChangerSimulator.Core.Models;

/// <summary>在庫管理と履歴管理を統合し、実務的な入出金操作を提供するマネージャークラス。</summary>
/// <param name="inventory">在庫管理オブジェクト。</param>
/// <param name="history">履歴管理オブジェクト。</param>
public class CashChangerManager(Inventory inventory, TransactionHistory history)
{
    private readonly Inventory _inventory = inventory;
    private readonly TransactionHistory _history = history;
    private readonly ILogger<CashChangerManager> _logger = LogProvider.CreateLogger<CashChangerManager>();
    private readonly ChangeCalculator _calculator = new();

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
        var counts = _calculator.Calculate(_inventory, amount, currencyCode);
        Dispense(counts);
    }
}
