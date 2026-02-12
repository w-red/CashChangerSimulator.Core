namespace CashChangerSimulator.Tests.Core;

using CashChangerSimulator.Core.Models;
using Shouldly;
using Xunit;

/// <summary>
/// Inventory クラスの基本機能を検証するテスト。
/// </summary>
public class InventoryTests
{
    /// <summary>
    /// 指定された金種の枚数を追加し、正しく保持されることを確認する。
    /// </summary>
    [Fact]
    public void Inventory_Add_ShouldIncreaseCount()
    {
        // Arrange
        var inventory = new Inventory();
        var denomination = 1000;

        // Act
        inventory.Add(denomination, 5);

        // Assert
        inventory.GetCount(denomination).ShouldBe(5);
    }

    /// <summary>
    /// 存在しない金種の枚数を取得した場合、0 が返されることを確認する。
    /// </summary>
    [Fact]
    public void Inventory_GetCount_NonExistent_ShouldReturnZero()
    {
        // Arrange
        var inventory = new Inventory();

        // Act
        var count = inventory.GetCount(500);

        // Assert
        count.ShouldBe(0);
    }

    /// <summary>
    /// 複数の金種がある場合、合計金額が正しく計算されることを確認する。
    /// </summary>
    [Fact]
    public void Inventory_TotalAmount_ShouldBeCorrect()
    {
        // Arrange
        var inventory = new Inventory();
        inventory.Add(1000, 2); // 2000
        inventory.Add(500, 3);  // 1500
        inventory.Add(100, 10); // 1000
        // Total: 4500

        // Act
        var total = inventory.CalculateTotal();

        // Assert
        total.ShouldBe(4500);
    }
}
