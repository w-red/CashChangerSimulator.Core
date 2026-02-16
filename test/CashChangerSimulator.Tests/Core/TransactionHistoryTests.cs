namespace CashChangerSimulator.Tests.Core;

using System;
using System.Collections.Generic;
using CashChangerSimulator.Core.Models;
using MoneyKind4Opos.Currencies.Interfaces;
using R3;
using Shouldly;
using Xunit;

/// <summary>
/// TransactionHistory の動作を検証するテスト。
/// </summary>
public class TransactionHistoryTests
{
    /// <summary>取引履歴が追加された際、正しく通知され、格納されることを検証する。</summary>
    [Fact]
    public void TransactionHistoryAddEntryShouldNotifyAndStore()
    {
        // Arrange
        var history = new TransactionHistory();
        var counts = new Dictionary<DenominationKey, int> 
        { 
            { new DenominationKey(1000, CashType.Bill), 2 }, 
            { new DenominationKey(500, CashType.Coin), 1 } 
        };
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
