
using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using Shouldly;

namespace CashChangerSimulator.Tests.Core;
/// <summary>CashChangerManager のビジネスロジックを検証するテスト。</summary>
public class CashChangerManagerTests
{
    /// <summary>入金時に在庫と履歴が正しく更新されることを検証する。</summary>
    [Fact]
    public void DepositShouldUpdateInventoryAndHistory()
    {
        // Arrange
        var inventory = new Inventory();
        var history = new TransactionHistory();
        var manager = new CashChangerManager(inventory, history, new ChangeCalculator());
        var b1000 = new DenominationKey(1000, CurrencyCashType.Bill);
        var c100 = new DenominationKey(100, CurrencyCashType.Coin);
        var depositCounts = new Dictionary<DenominationKey, int> { { b1000, 2 }, { c100, 5 } };

        // Act
        manager.Deposit(depositCounts);

        // Assert: Inventory updated
        inventory.GetCount(b1000).ShouldBe(2);
        inventory.GetCount(c100).ShouldBe(5);
        inventory.CalculateTotal().ShouldBe(2500m);

        // Assert: History added
        history.Entries.Count.ShouldBe(1);
        var entry = history.Entries[0];
        entry.Type.ShouldBe(TransactionType.Deposit);
        entry.Amount.ShouldBe(2500m);
        entry.Counts[b1000].ShouldBe(2);
        entry.Counts[c100].ShouldBe(5);
    }

    /// <summary>払出時に在庫と履歴が正しく更新されることを検証する。</summary>
    [Fact]
    public void DispenseShouldUpdateInventoryAndHistory()
    {
        // Arrange
        var inventory = new Inventory();
        var b1000 = new DenominationKey(1000, CurrencyCashType.Bill);
        inventory.SetCount(b1000, 10);
        var history = new TransactionHistory();
        var manager = new CashChangerManager(inventory, history, new ChangeCalculator());
        var dispenseCounts = new Dictionary<DenominationKey, int> { { b1000, 3 } };

        // Act
        manager.Dispense(dispenseCounts);

        // Assert: Inventory updated
        inventory.GetCount(b1000).ShouldBe(7);

        // Assert: History added
        history.Entries.Count.ShouldBe(1);
        var entry = history.Entries[0];
        entry.Type.ShouldBe(TransactionType.Dispense);
        entry.Amount.ShouldBe(3000m);
    }

    /// <summary>金額指定の払出時に計算、更新、記録が正しく行われることを検証する。</summary>
    [Fact]
    public void DispenseByAmountShouldCalculateUpdateAndRecord()
    {
        // Arrange
        var inventory = new Inventory();
        var b1000 = new DenominationKey(1000, CurrencyCashType.Bill);
        var c100 = new DenominationKey(100, CurrencyCashType.Coin);
        inventory.SetCount(b1000, 5);
        inventory.SetCount(c100, 10);
        var history = new TransactionHistory();
        var manager = new CashChangerManager(inventory, history, new ChangeCalculator());

        // Act: 1200円を出金
        manager.Dispense(1200m);

        // Assert: Inventory updated
        inventory.GetCount(b1000).ShouldBe(4);
        inventory.GetCount(c100).ShouldBe(8);

        // Assert: History recorded
        history.Entries.Count.ShouldBe(1);
        history.Entries[0].Amount.ShouldBe(1200m);
        history.Entries[0].Counts[b1000].ShouldBe(1);
        history.Entries[0].Counts[c100].ShouldBe(2);
    }

    /// <summary>多通貨（USD）での入金と履歴記録を検証する。</summary>
    [Fact]
    public void DepositWithOtherCurrencyShouldStoreCorrectCurrencyCode()
    {
        // Arrange
        var inventory = new Inventory();
        var history = new TransactionHistory();
        var manager = new CashChangerManager(inventory, history, new ChangeCalculator());
        var usd20 = new DenominationKey(20, CurrencyCashType.Bill, "USD");
        var depositCounts = new Dictionary<DenominationKey, int> { { usd20, 2 } };

        // Act
        manager.Deposit(depositCounts);

        // Assert
        inventory.GetCount(usd20).ShouldBe(2);
        inventory.CalculateTotal("USD").ShouldBe(40m);
        history.Entries[0].Counts.Keys.ShouldContain(k => k.CurrencyCode == "USD");
    }

    /// <summary>在庫不足時に払出が失敗し、在庫と履歴が更新されないことを検証する。</summary>
    [Fact]
    public void DispenseByAmountWithInsufficientCashShouldThrowAndNotModifyState()
    {
        // Arrange
        var inventory = new Inventory(); // Empty
        var history = new TransactionHistory();
        var manager = new CashChangerManager(inventory, history, new ChangeCalculator());

        // Act & Assert
        Should.Throw<InsufficientCashException>(() => manager.Dispense(100m));
        
        inventory.CalculateTotal().ShouldBe(0m);
        history.Entries.ShouldBeEmpty();
    }
}
