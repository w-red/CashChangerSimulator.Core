namespace CashChangerSimulator.Tests.Core;

using System.Collections.Generic;
using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Models;
using Shouldly;
using Xunit;

/// <summary>
/// お釣り計算ロジック（欲張り法）を検証するテスト。
/// </summary>
public class ChangeCalculatorTests
{
    [Fact]
    public void Calculate_WithSufficientInventory_ShouldReturnCorrectCounts()
    {
        // Arrange
        var inventory = new Inventory();
        inventory.SetCount(1000, 10);
        inventory.SetCount(500, 10);
        inventory.SetCount(100, 10);
        inventory.SetCount(50, 10);
        inventory.SetCount(10, 10);
        
        var calculator = new ChangeCalculator();

        // Act: 1860円を計算
        var result = calculator.Calculate(inventory, 1860m);

        // Assert
        result[1000].ShouldBe(1);
        result[500].ShouldBe(1);
        result[100].ShouldBe(3);
        result[50].ShouldBe(1);
        result[10].ShouldBe(1);
    }

    [Fact]
    public void Calculate_WithInsufficientInventory_ShouldThrowException()
    {
        // Arrange
        var inventory = new Inventory();
        inventory.SetCount(100, 2); // 200円分のみ
        
        var calculator = new ChangeCalculator();

        // Act & Assert
        Should.Throw<InsufficientCashException>(() => 
            calculator.Calculate(inventory, 300m)
        );
    }

    [Fact]
    public void Calculate_WhenExactAmountImpossible_ShouldThrowException()
    {
        // Arrange
        var inventory = new Inventory();
        inventory.SetCount(100, 10);
        // 50円玉がない
        
        var calculator = new ChangeCalculator();

        // Act & Assert: 150円は払えない
        Should.Throw<InsufficientCashException>(() => 
            calculator.Calculate(inventory, 150m)
        );
    }
}
