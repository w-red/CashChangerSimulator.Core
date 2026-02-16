namespace CashChangerSimulator.Tests.Core;

using System.Collections.Generic;
using System.Linq;
using CashChangerSimulator.Core.Models;
using MoneyKind4Opos.Currencies.Interfaces;
using Shouldly;
using Xunit;

/// <summary>
/// CashChangerManager のビジネスロジックを検証するテスト。
/// </summary>
public class CashChangerManagerTests
{
    /// <summary>入金時に在庫と履歴が正しく更新されることを検証する。</summary>
    [Fact]
    public void DepositShouldUpdateInventoryAndHistory()
    {
        // Arrange
        var inventory = new Inventory();
        var history = new TransactionHistory();
        var manager = new CashChangerManager(inventory, history);
        var b1000 = new DenominationKey(1000, CashType.Bill);
        var c100 = new DenominationKey(100, CashType.Coin);
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
        var b1000 = new DenominationKey(1000, CashType.Bill);
        inventory.SetCount(b1000, 10);
        var history = new TransactionHistory();
        var manager = new CashChangerManager(inventory, history);
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
        var b1000 = new DenominationKey(1000, CashType.Bill);
        var c100 = new DenominationKey(100, CashType.Coin);
        inventory.SetCount(b1000, 5);
        inventory.SetCount(c100, 10);
        var history = new TransactionHistory();
        var manager = new CashChangerManager(inventory, history);

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
}
