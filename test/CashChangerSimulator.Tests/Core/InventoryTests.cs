namespace CashChangerSimulator.Tests.Core;

using CashChangerSimulator.Core.Models;
using MoneyKind4Opos.Currencies.Interfaces;
using R3;
using Shouldly;
using Xunit;

/// <summary>
/// Inventory クラスの基本機能を検証するテスト。
/// </summary>
public class InventoryTests
{
    /// <summary>指定された金種の枚数を追加し、正しく保持されることを検証する。</summary>
    [Fact]
    public void InventoryAddShouldIncreaseCount()
    {
        // Arrange
        var inventory = new Inventory();
        var denomination = new DenominationKey(1000, CashType.Bill);

        // Act
        inventory.Add(denomination, 5);

        // Assert
        inventory.GetCount(denomination).ShouldBe(5);
    }

    /// <summary>存在しない金種の枚数を取得した場合、0 が返されることを検証する。</summary>
    [Fact]
    public void InventoryGetCountNonExistentShouldReturnZero()
    {
        // Arrange
        var inventory = new Inventory();
        var denomination = new DenominationKey(500, CashType.Coin);

        // Act
        var count = inventory.GetCount(denomination);

        // Assert
        count.ShouldBe(0);
    }

    /// <summary>複数の金種がある場合、合計金額が正しく計算されることを検証する。</summary>
    [Fact]
    public void InventoryTotalAmountShouldBeCorrect()
    {
        // Arrange
        var inventory = new Inventory();
        inventory.Add(new DenominationKey(1000, CashType.Bill), 2); // 2000
        inventory.Add(new DenominationKey(500, CashType.Coin), 3);  // 1500
        inventory.Add(new DenominationKey(100, CashType.Coin), 10); // 1000
        // Total: 4500

        // Act
        var total = inventory.CalculateTotal();

        // Assert
        total.ShouldBe(4500m);
    }

    /// <summary>在庫が追加された際、Changed ストリームに通知が飛ぶことを検証する。</summary>
    [Fact]
    public void InventoryAddShouldNotifyChanged()
    {
        // Arrange
        var inventory = new Inventory();
        var denomination = new DenominationKey(100, CashType.Coin);
        DenominationKey? notifiedDenomination = null;
        using var _ = inventory.Changed.Subscribe(d => notifiedDenomination = d);

        // Act
        inventory.Add(denomination, 1);

        // Assert
        notifiedDenomination.ShouldBe(denomination);
    }

    /// <summary>在庫をディクショナリに変換し、再び読み込めることを検証する。</summary>
    [Fact]
    public void InventoryShouldSerializeAndDeserializeViaDictionary()
    {
        // Arrange
        var inventory = new Inventory();
        var b1000 = new DenominationKey(1000, CashType.Bill);
        var c100 = new DenominationKey(100, CashType.Coin);
        inventory.SetCount(b1000, 10);
        inventory.SetCount(c100, 50);

        // Act
        var dict = inventory.ToDictionary();
        var newInventory = new Inventory();
        newInventory.LoadFromDictionary(dict);

        // Assert
        dict.ShouldContainKeyAndValue("B1000", 10);
        dict.ShouldContainKeyAndValue("C100", 50);
        newInventory.GetCount(b1000).ShouldBe(10);
        newInventory.GetCount(c100).ShouldBe(50);
    }
}
