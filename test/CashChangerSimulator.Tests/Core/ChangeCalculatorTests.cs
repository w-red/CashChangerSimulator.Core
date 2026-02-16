namespace CashChangerSimulator.Tests.Core;

using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Models;
using MoneyKind4Opos.Currencies.Interfaces;
using Shouldly;
using Xunit;

/// <summary>
/// お釣り計算ロジック（欲張り法）を検証するテスト。
/// </summary>
public class ChangeCalculatorTests
{
    /// <summary>在庫が十分にある場合、お釣り計算が正しい枚数を返すことを検証する。</summary>
    [Fact]
    public void CalculateWithSufficientInventoryShouldReturnCorrectCounts()
    {
        // Arrange
        var inventory = new Inventory();
        inventory.SetCount(new DenominationKey(1000, CashType.Bill), 10);
        inventory.SetCount(new DenominationKey(500, CashType.Coin), 10);
        inventory.SetCount(new DenominationKey(100, CashType.Coin), 10);
        inventory.SetCount(new DenominationKey(50, CashType.Coin), 10);
        inventory.SetCount(new DenominationKey(10, CashType.Coin), 10);
        
        var calculator = new ChangeCalculator();

        // Act: 1860円を計算
        var result = calculator.Calculate(inventory, 1860m);

        // Assert
        result[new DenominationKey(1000, CashType.Bill)].ShouldBe(1);
        result[new DenominationKey(500, CashType.Coin)].ShouldBe(1);
        result[new DenominationKey(100, CashType.Coin)].ShouldBe(3);
        result[new DenominationKey(50, CashType.Coin)].ShouldBe(1);
        result[new DenominationKey(10, CashType.Coin)].ShouldBe(1);
    }

    /// <summary>在庫が不足している場合、例外がスローされることを検証する。</summary>
    [Fact]
    public void CalculateWithInsufficientInventoryShouldThrowException()
    {
        // Arrange
        var inventory = new Inventory();
        inventory.SetCount(new DenominationKey(100, CashType.Coin), 2); // 200円分のみ
        
        var calculator = new ChangeCalculator();

        // Act & Assert
        Should.Throw<InsufficientCashException>(() => 
            calculator.Calculate(inventory, 300m)
        );
    }

    /// <summary>正確な金額の払い出しが不可能な場合、例外がスローされることを検証する。</summary>
    [Fact]
    public void CalculateWhenExactAmountImpossibleShouldThrowException()
    {
        // Arrange
        var inventory = new Inventory();
        inventory.SetCount(new DenominationKey(100, CashType.Coin), 10);
        // 50円玉がない
        
        var calculator = new ChangeCalculator();

        // Act & Assert: 150円は払えない
        Should.Throw<InsufficientCashException>(() => 
            calculator.Calculate(inventory, 150m)
        );
    }
}
