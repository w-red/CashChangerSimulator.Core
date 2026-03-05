using CashChangerSimulator.Core.Models;
using R3;
using Shouldly;

namespace CashChangerSimulator.Tests.Core;

/// <summary>Inventory クラスの基本機能を検証するテスト。</summary>
public class InventoryTests
{
    /// <summary>指定された金種の枚数を追加し、正しく保持されることを検証する。</summary>
    [Fact]
    public void InventoryAddShouldIncreaseCount()
    {
        // Arrange
        var inventory = new Inventory();
        var denomination = new DenominationKey(1000, CurrencyCashType.Bill);

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
        var denomination = new DenominationKey(500, CurrencyCashType.Coin);

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
        inventory.Add(new DenominationKey(1000, CurrencyCashType.Bill), 2); // 2000
        inventory.Add(new DenominationKey(500, CurrencyCashType.Coin), 3);  // 1500
        inventory.Add(new DenominationKey(100, CurrencyCashType.Coin), 10); // 1000
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
        var denomination = new DenominationKey(100, CurrencyCashType.Coin);
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
        var b1000 = new DenominationKey(1000, CurrencyCashType.Bill);
        var c100 = new DenominationKey(100, CurrencyCashType.Coin);
        inventory.SetCount(b1000, 10);
        inventory.SetCount(c100, 50);

        // Act
        var dict = inventory.ToDictionary();
        var newInventory = new Inventory();
        newInventory.LoadFromDictionary(dict);

        // Assert
        dict.ShouldContainKeyAndValue("JPY:B1000", 10);
        dict.ShouldContainKeyAndValue("JPY:C100", 50);
        newInventory.GetCount(b1000).ShouldBe(10);
        newInventory.GetCount(c100).ShouldBe(50);
    }
    /// <summary>負の枚数を追加しようとした場合、無視されることを検証する。</summary>
    [Fact]
    public void InventoryAddNegativeShouldBeIgnored()
    {
        // Arrange
        var inventory = new Inventory();
        var denomination = new DenominationKey(1000, CurrencyCashType.Bill);

        // Act
        inventory.Add(denomination, -5);

        // Assert
        inventory.GetCount(denomination).ShouldBe(0);
    }

    /// <summary>負の枚数を設定しようとした場合、無視されることを検証する。</summary>
    [Fact]
    public void InventorySetCountNegativeShouldBeIgnored()
    {
        // Arrange
        var inventory = new Inventory();
        var denomination = new DenominationKey(1000, CurrencyCashType.Bill);
        inventory.SetCount(denomination, 10);

        // Act
        inventory.SetCount(denomination, -1);

        // Assert
        inventory.GetCount(denomination).ShouldBe(10);
    }

    /// <summary>非常に大きな枚数を扱えることを検証する（境界値テスト）。</summary>
    [Fact]
    public void InventoryShouldHandleLargeCounts()
    {
        // Arrange
        var inventory = new Inventory();
        var denomination = new DenominationKey(1000, CurrencyCashType.Bill);
        int largeCount = 1_000_000;

        // Act
        inventory.SetCount(denomination, largeCount);

        // Assert
        inventory.GetCount(denomination).ShouldBe(largeCount);
        inventory.CalculateTotal().ShouldBe(1_000_000_000m);
    }
}
