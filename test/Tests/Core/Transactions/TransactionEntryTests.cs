using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Transactions;
using Shouldly;

namespace CashChangerSimulator.Tests.Core.Transactions;

/// <summary>取引エントリ（TransactionEntry）の不変性と 'with' 式によるプロパティ変更を検証するテストクラス。</summary>
public class TransactionEntryTests
{
    /// <summary>'with' 式を使用して init セッター経由でプロパティが正しく変更されることを検証します。</summary>
    [Fact]
    public void TransactionEntry_InitSetters_CanBeUsedViaWithExpression()
    {
        // Arrange
        var dict1 = new Dictionary<DenominationKey, int> { { new DenominationKey(1000, CurrencyCashType.Bill), 1 } };
        var dict2 = new Dictionary<DenominationKey, int> { { new DenominationKey(500, CurrencyCashType.Coin), 1 } };
        var originalTime = DateTimeOffset.Now.AddDays(-1);
        var newTime = DateTimeOffset.Now;

        var original = new TransactionEntry(originalTime, TransactionType.Deposit, 1000, dict1);

        // Act - Use the 'with' expression to invoke the init setters (set_Amount, set_Type, etc.)
        var mutated = original with
        {
            Timestamp = newTime,
            Type = TransactionType.Dispense,
            Amount = 500,
            Counts = dict2
        };

        // Assert
        mutated.Timestamp.ShouldBe(newTime);
        mutated.Type.ShouldBe(TransactionType.Dispense);
        mutated.Amount.ShouldBe(500);
        mutated.Counts.ShouldBeSameAs(dict2);

        // Ensure original is unchanged (immutability)
        original.Timestamp.ShouldBe(originalTime);
        original.Amount.ShouldBe(1000);
        original.Type.ShouldBe(TransactionType.Deposit);
    }
}
