namespace CashChangerSimulator.Tests.Core;

using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using MoneyKind4Opos.Currencies.Interfaces;
using R3;
using Shouldly;
using System;
using System.Collections.Generic;
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

    /// <summary>1000件を超える履歴が追加された際、古い順に破棄されることを検証する。</summary>
    [Fact]
    public void TransactionHistoryShouldEnforceLimit()
    {
        var history = new TransactionHistory();
        for (int i = 0; i < 1100; i++)
        {
            history.Add(new TransactionEntry(DateTimeOffset.Now, TransactionType.Adjustment, i, new Dictionary<DenominationKey, int>()));
        }

        history.Entries.Count.ShouldBe(1000);
        history.Entries[0].Amount.ShouldBe(1099); // 最新
        history.Entries[999].Amount.ShouldBe(100); // 100番目が最後（100-1099の1000件）
    }

    /// <summary>HistoryState DTO との相互変換が正しく行われることを検証する。</summary>
    [Fact]
    public void TransactionHistoryShouldConvertStateCorrectly()
    {
        var history = new TransactionHistory();
        var key = new DenominationKey(1000, CashType.Bill, "JPY");
        history.Add(new TransactionEntry(DateTimeOffset.Now, TransactionType.Refill, 5000m, new Dictionary<DenominationKey, int> { { key, 5 } }));

        var state = history.ToState();
        state.Entries.Count.ShouldBe(1);
        state.Entries[0].Counts.ContainsKey("JPY:B1000").ShouldBeTrue();

        var history2 = new TransactionHistory();
        history2.FromState(state);
        history2.Entries.Count.ShouldBe(1);
        history2.Entries[0].Amount.ShouldBe(5000m);
        history2.Entries[0].Type.ShouldBe(TransactionType.Refill);
        history2.Entries[0].Counts[key].ShouldBe(5);
    }

    /// <summary>Entries が null の HistoryState を渡してもクラッシュしないことを検証する。</summary>
    [Fact]
    public void FromStateWithNullEntriesShouldNotCrash()
    {
        var history = new TransactionHistory();
        var state = new HistoryState { Entries = null! };

        // Act & Assert
        Should.NotThrow(() => history.FromState(state));
        history.Entries.ShouldBeEmpty();
    }
}
