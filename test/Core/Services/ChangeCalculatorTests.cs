using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Core.Services;

/// <summary>出金計算ロジック（最適な金種構成の算出、通貨フィルタ等）を検証するテストクラス。</summary>
public class ChangeCalculatorTests
{

    /// <summary>カスタムフィルタ（例：紙幣のみ）が指定された際に、条件に合う金種のみで計算されることを検証します。</summary>
    [Fact]
    public void CalculateWithCustomFilterShouldWork()
    {
        var inv = Inventory.Create();
        inv.Add(new DenominationKey(1000, CurrencyCashType.Bill, "JPY"), 10);
        inv.Add(new DenominationKey(500, CurrencyCashType.Coin, "JPY"), 10);

        // Filter: Only bills. 1500 cannot be paid fully with only 1000 bills.
        Should.Throw<InsufficientCashException>(() => ChangeCalculator.Calculate(inv, 1500, filter: k => k.Type == CurrencyCashType.Bill));
    }

    /// <summary>在庫不足により出金計算が不可能な場合に InsufficientCashException がスローされることを検証します。</summary>
    [Fact]
    public void CalculateInsufficientCashShouldThrow()
    {
        var inv = Inventory.Create();
        inv.Add(new DenominationKey(1000, CurrencyCashType.Bill, "JPY"), 1);

        Should.Throw<InsufficientCashException>(() => ChangeCalculator.Calculate(inv, 1500));
    }

    /// <summary>混合した金種在庫から、最適な（枚数が最小になる）組み合わせが算出されることを検証します。</summary>
    [Fact]
    public void CalculateOptimalCombinationShouldWork()
    {
        var inv = Inventory.Create();
        var k1000 = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        var k500 = new DenominationKey(500, CurrencyCashType.Coin, "JPY");
        var k100 = new DenominationKey(100, CurrencyCashType.Coin, "JPY");
        var k10 = new DenominationKey(10, CurrencyCashType.Coin, "JPY");
        var k1 = new DenominationKey(1, CurrencyCashType.Coin, "JPY");

        inv.Add(k1000, 10);
        inv.Add(k500, 10);
        inv.Add(k100, 10);
        inv.Add(k10, 10);
        inv.Add(k1, 10);

        // 6666 JPY = 1000x6, 500x1, 100x1, 10x6, 1x6
        var result = ChangeCalculator.Calculate(inv, 6666);

        result.Count.ShouldBe(5);
        result[k1000].ShouldBe(6);
        result[k500].ShouldBe(1);
        result[k100].ShouldBe(1);
        result[k10].ShouldBe(6);
        result[k1].ShouldBe(6);

        // Strict validation: No zero-count entries should be present
        result.Values.All(v => v > 0).ShouldBeTrue();
    }

    /// <summary>一部の高額金種が不足している場合に、次に高額な金種で補完されることを検証します。</summary>
    [Fact]
    public void CalculateWithPartialInventoryShouldFollowGreedy()
    {
        var inv = Inventory.Create();
        var k1000 = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        var k500 = new DenominationKey(500, CurrencyCashType.Coin, "JPY");

        inv.Add(k1000, 1); // Only one 1000 bill
        inv.Add(k500, 10);

        // Request 2000. Should take 1000x1 and 500x2.
        var result = ChangeCalculator.Calculate(inv, 2000);

        result[k1000].ShouldBe(1);
        result[k500].ShouldBe(2);
        result.Values.All(v => v > 0).ShouldBeTrue();
    }

    /// <summary>通貨コードによるフィルタリングが正しく機能することを検証します。</summary>
    [Fact]
    public void ShouldFilterByCurrencyMatching()
    {
        var inv = Inventory.Create();
        var keyJpy = new DenominationKey(100, CurrencyCashType.Coin, "JPY");
        var keyUsd = new DenominationKey(100, CurrencyCashType.Coin, "USD");
        inv.Add(keyJpy, 10);
        inv.Add(keyUsd, 10);

        // Should only pick JPY keys
        var result = ChangeCalculator.Calculate(inv, 100m, currencyCode: "JPY");

        result.Count.ShouldBe(1);
        result.ContainsKey(keyJpy).ShouldBeTrue();
        result.ContainsKey(keyUsd).ShouldBeFalse();
    }

    /// <summary>額面が同じ場合でも金種種別（Type）による優先順位が守られることを検証します。</summary>
    [Fact]
    public void ShouldRespectTypePriorityForSameValue()
    {
        var inv = Inventory.Create();
        // Create bill and coin with same value 1000
        var kBill = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        var kCoin = new DenominationKey(1000, CurrencyCashType.Coin, "JPY");
        inv.Add(kBill, 1);
        inv.Add(kCoin, 1);

        // Calculate 1000. 
        // ThenByDescending(k => k.Type) should pick Bill (higher enum value) before Coin.
        var result = ChangeCalculator.Calculate(inv, 1000);

        result.Count.ShouldBe(1);
        result.Keys.First().Type.ShouldBe(CurrencyCashType.Bill);
    }

    /// <summary>Inventory クラス以外の IReadOnlyInventory 実装を渡した際のエラーハンドリングを検証します。</summary>
    [Fact]
    public void CalculateWithNonInventoryTypeShouldReturnEmpty()
    {
        // GetAvailableDenominationKeys uses 'inventory is Inventory'
        // If we pass a mock IReadOnlyInventory, it should return empty list of keys
        var mockInv = new Mock<IReadOnlyInventory>();
        mockInv.Setup(m => m.GetCount(It.IsAny<DenominationKey>())).Returns(10);

        // Should throw because no keys are available from the mock (it's not an Inventory instance)
        Should.Throw<InsufficientCashException>(() => ChangeCalculator.Calculate(mockInv.Object, 100));
    }

    /// <summary>引数に null を渡した場合に ArgumentNullException がスローされることを検証します（Mutant 812 撃破）。</summary>
    [Fact]
    public void Calculate_ShouldThrowArgumentNullException_WhenInventoryIsNull()
    {
        Should.Throw<ArgumentNullException>(() => ChangeCalculator.Calculate(null!, 100));
    }

    /// <summary>
    /// 必要ない金種（needed &lt;= 0）がディクショナリに含まれないことを検証します（Mutant 791, 796 撃破）。
    /// また、計算が完了した後に余剰な金種の GetCount を呼び出さないことを検証します（Mutant 785, 788 撃破）。
    /// </summary>
    [Fact]
    public void Calculate_ShouldBeEfficientAndNotIncludeZeroCounts()
    {
        var inv = new SpyInventory();
        var k1000 = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        var k100 = new DenominationKey(100, CurrencyCashType.Coin, "JPY");
        var k1 = new DenominationKey(1, CurrencyCashType.Coin, "JPY");

        inv.Add(k1000, 10);
        inv.Add(k100, 10);
        inv.Add(k1, 10);

        // Request 100.
        // 1. 1000 is checked, needed = 0 -> should continue (Mutant 788, 791)
        // 2. 100 is checked, needed = 1 -> took 1. Remaining = 0.
        // 3. remaining is 0 -> should break (Mutant 785)
        var result = ChangeCalculator.Calculate(inv, 100);

        // Check dictionary integrity
        result.Count.ShouldBe(1);
        result.ContainsKey(k100).ShouldBeTrue();
        result.ContainsKey(k1000).ShouldBeFalse(); // Should NOT contain k1000 with count 0 (Mutant 796)
        result.ContainsKey(k1).ShouldBeFalse();

        // Check efficiency (Calls to GetCount)
        inv.GetCountCalls.ShouldNotContain(k1); // mutant 785 (break) prevents checking k1
        inv.GetCountCalls.ShouldNotContain(k1000); // mutant 788/791 (continue) should prevent getting count if not needed
        inv.GetCountCalls.ShouldContain(k100);
    }

    /// <summary>呼び出し回数を追跡するためのテスト用 Inventory クラス。</summary>
    private class SpyInventory : Inventory
    {
        public List<DenominationKey> GetCountCalls { get; } = [];

        public override int GetCount(DenominationKey key)
        {
            GetCountCalls.Add(key);
            return base.GetCount(key);
        }
    }
}
