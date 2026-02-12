namespace CashChangerSimulator.Tests.Core;

using System.Collections.Generic;
using System.Linq;
using CashChangerSimulator.Core.Models;
using Shouldly;
using Xunit;

/// <summary>
/// CashChangerManager のビジネスロジックを検証するテスト。
/// </summary>
public class CashChangerManagerTests
{
    [Fact]
    public void Deposit_ShouldUpdateInventoryAndHistory()
    {
        // Arrange
        var inventory = new Inventory();
        var history = new TransactionHistory();
        var manager = new CashChangerManager(inventory, history);
        var depositCounts = new Dictionary<int, int> { { 1000, 2 }, { 100, 5 } };

        // Act
        manager.Deposit(depositCounts);

        // Assert: Inventory updated
        inventory.GetCount(1000).ShouldBe(2);
        inventory.GetCount(100).ShouldBe(5);
        inventory.CalculateTotal().ShouldBe(2500m);

        // Assert: History added
        history.Entries.Count.ShouldBe(1);
        var entry = history.Entries[0];
        entry.Type.ShouldBe(TransactionType.Deposit);
        entry.Amount.ShouldBe(2500m);
        entry.Counts[1000].ShouldBe(2);
        entry.Counts[100].ShouldBe(5);
    }

    [Fact]
    public void Dispense_ShouldUpdateInventoryAndHistory()
    {
        // Arrange
        var inventory = new Inventory();
        inventory.SetCount(1000, 10);
        var history = new TransactionHistory();
        var manager = new CashChangerManager(inventory, history);
        var dispenseCounts = new Dictionary<int, int> { { 1000, 3 } };

        // Act
        manager.Dispense(dispenseCounts);

        // Assert: Inventory updated
        inventory.GetCount(1000).ShouldBe(7);

        // Assert: History added
        history.Entries.Count.ShouldBe(1);
        var entry = history.Entries[0];
        entry.Type.ShouldBe(TransactionType.Dispense);
        entry.Amount.ShouldBe(3000m);
    }

    [Fact]
    public void Dispense_ByAmount_ShouldCalculateUpdateAndRecord()
    {
        // Arrange
        var inventory = new Inventory();
        inventory.SetCount(1000, 5);
        inventory.SetCount(100, 10);
        var history = new TransactionHistory();
        var manager = new CashChangerManager(inventory, history);

        // Act: 1200円を出金
        manager.Dispense(1200m);

        // Assert: Inventory updated
        inventory.GetCount(1000).ShouldBe(4);
        inventory.GetCount(100).ShouldBe(8);

        // Assert: History recorded
        history.Entries.Count.ShouldBe(1);
        history.Entries[0].Amount.ShouldBe(1200m);
        history.Entries[0].Counts[1000].ShouldBe(1);
        history.Entries[0].Counts[100].ShouldBe(2);
    }
}
