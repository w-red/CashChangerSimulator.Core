namespace CashChangerSimulator.Tests.Core;

using System;
using System.Collections.Generic;
using CashChangerSimulator.Core.Models;
using R3;
using Shouldly;
using Xunit;

/// <summary>
/// TransactionHistory の動作を検証するテスト。
/// </summary>
public class TransactionHistoryTests
{
    [Fact]
    public void TransactionHistory_AddEntry_ShouldNotifyAndStore()
    {
        // Arrange
        var history = new TransactionHistory();
        var counts = new Dictionary<int, int> { { 1000, 2 }, { 500, 1 } };
        var entry = new TransactionEntry(DateTimeOffset.Now, TransactionType.Deposit, 2500m, counts);
        
        TransactionEntry? lastNotified = null;
        using var _ = history.Added.Subscribe(e => lastNotified = e);

        // Act
        history.Add(entry);

        // Assert
        lastNotified.ShouldNotBeNull();
        lastNotified.Amount.ShouldBe(2500m);
        history.Entries.Count.ShouldBe(1);
        history.Entries[0].Type.ShouldBe(TransactionType.Deposit);
    }
}
