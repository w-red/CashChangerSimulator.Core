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
    /// <summary>負の枚数を追加（減算）した場合、正しく減算され、0未満にはならないことを検証する。</summary>
    [Fact]
    public void InventoryAddNegativeShouldSubtractAndClampToZero()
    {
        // Arrange
        var inventory = new Inventory();
        var denomination = new DenominationKey(1000, CurrencyCashType.Bill);
        inventory.SetCount(denomination, 10);

        // Act & Assert: Subtraction works
        inventory.Add(denomination, -5);
        inventory.GetCount(denomination).ShouldBe(5);

        // Act & Assert: Clamping works
        inventory.Add(denomination, -10);
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

    /// <summary>空の通貨コードを持つキーが、SetCount でも正規化されることを検証する（現状は失敗する想定）。</summary>
    [Fact]
    public void InventorySetCountShouldNormalizeEmptyCurrency()
    {
        // Arrange
        var inventory = new Inventory();
        var emptyCurrencyKey = new DenominationKey(1000, CurrencyCashType.Bill, "");
        var normalizedKey = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");

        // Act
        inventory.SetCount(emptyCurrencyKey, 10);

        // Assert
        // GetCount(normalizedKey) で取得できるべきだが、現状の実装では正規化されないため失敗する
        inventory.GetCount(normalizedKey).ShouldBe(10);
    }

    /// <summary>空の通貨コードを持つキーが、AddCollection/AddReject でも正規化されることを検証する（現状は失敗する想定）。</summary>
    [Fact]
    public void InventoryAddCollectionAndRejectShouldNormalizeEmptyCurrency()
    {
        // Arrange
        var inventory = new Inventory();
        var emptyCurrencyKey = new DenominationKey(1000, CurrencyCashType.Bill, "");
        var normalizedKey = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");

        // Act
        inventory.AddCollection(emptyCurrencyKey, 1);
        inventory.AddReject(emptyCurrencyKey, 2);

        // Assert
        inventory.CollectionCounts.ShouldContain(kv => kv.Key == normalizedKey && kv.Value == 1);
        inventory.RejectCounts.ShouldContain(kv => kv.Key == normalizedKey && kv.Value == 2);
    }
    /// <summary>HasDiscrepancy の状態遷移（Collection/Reject への追加、Clear、強制設定）を検証する。</summary>
    [Fact]
    public void InventoryHasDiscrepancyShouldTransitionCorrectly()
    {
        // Arrange
        var inventory = new Inventory();
        var denomination = new DenominationKey(100, CurrencyCashType.Coin);
 
        // 初期状態
        inventory.HasDiscrepancy.ShouldBeFalse();
 
        // Act: 回収庫へ追加
        inventory.AddCollection(denomination, 1);
        inventory.HasDiscrepancy.ShouldBeTrue("Collection exists");
 
        // Act: クリア
        inventory.Clear();
        inventory.HasDiscrepancy.ShouldBeFalse("Cleared counts");
 
        // Act: リジェクト庫へ追加
        inventory.AddReject(denomination, 1);
        inventory.HasDiscrepancy.ShouldBeTrue("Reject exists");
 
        // Act: 強制設定
        inventory.Clear();
        inventory.HasDiscrepancy = true;
        inventory.HasDiscrepancy.ShouldBeTrue("Forced discrepancy");
 
        inventory.HasDiscrepancy = false;
        inventory.HasDiscrepancy.ShouldBeFalse("Forced back to false");
    }
 
    /// <summary>複数通貨が存在する場合、通貨コードによるフィルタリングが正しく機能することを検証する。</summary>
    [Fact]
    public void InventoryCalculateTotalShouldFilterByCurrency()
    {
        // Arrange
        var inventory = new Inventory();
        inventory.Add(new DenominationKey(100, CurrencyCashType.Coin, "JPY"), 10); // 1000
        inventory.Add(new DenominationKey(1, CurrencyCashType.Coin, "USD"), 5);   // 5
 
        // Act & Assert
        inventory.CalculateTotal("JPY").ShouldBe(1000m);
        inventory.CalculateTotal("USD").ShouldBe(5m);
        inventory.CalculateTotal().ShouldBe(1005m);
        inventory.CalculateTotal("EUR").ShouldBe(0m);
    }
 
    /// <summary>LoadFromDictionary に不正な形式が含まれていても、他のデータが正常に読み込めることを検証する。</summary>
    [Fact]
    public void InventoryLoadFromDictionaryShouldBeRobustToErrors()
    {
        // Arrange
        var inventory = new Inventory();
        var data = new Dictionary<string, int>
        {
            { "JPY:C100", 10 },
            { "INVALID_KEY_FORMAT", 5 },
            { "COL:JPY:B1000", 2 }
        };
 
        // Act
        inventory.LoadFromDictionary(data);
 
        // Assert
        inventory.GetCount(new DenominationKey(100, CurrencyCashType.Coin, "JPY")).ShouldBe(10);
        inventory.CollectionCounts.ShouldContain(kv => kv.Key.Value == 1000 && kv.Value == 2);
    }
}
