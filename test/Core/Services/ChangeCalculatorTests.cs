using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Core.Services;

/// <summary>出金計算ロジック(最適な金種構成の算出、通貨フィルター)を検証するテストクラス。</summary>
public class ChangeCalculatorTests : IDisposable
{
    private readonly Inventory inv;

    public ChangeCalculatorTests()
    {
        inv = Inventory.Create();
    }

    /// <summary>カスタムフィルタ(例：紙幣のみ)が指定された際に、条件に合う金種のみで計算されることを検証します。</summary>
    [Fact(Timeout = 5000)]
    public void CalculateWithCustomFilterShouldWork()
    {
        inv.Add(new DenominationKey(1000, CurrencyCashType.Bill, "JPY"), 10);
        inv.Add(new DenominationKey(500, CurrencyCashType.Coin, "JPY"), 10);

        // Filter: Only bills. 1500 cannot be paid fully with only 1000 bills.
        Should.Throw<InsufficientCashException>(() => ChangeCalculator.Calculate(inv, 1500, filter: k => k.Type == CurrencyCashType.Bill));
    }

    /// <summary>在庫不足により出金計算が不可能な場合に InsufficientCashException がスローされることを検証します。</summary>
    [Fact(Timeout = 5000)]
    public void CalculateInsufficientCashShouldThrow()
    {
        inv.Add(new DenominationKey(1000, CurrencyCashType.Bill, "JPY"), 1);

        Should.Throw<InsufficientCashException>(() => ChangeCalculator.Calculate(inv, 1500));
    }

    /// <summary>混合した金種在庫から、最適な(枚数が最小になる)金種組み合わせが算出されることを検証します。</summary>
    [Fact(Timeout = 5000)]
    public void CalculateOptimalCombinationShouldWork()
    {
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

        var result = ChangeCalculator.Calculate(inv, 1666);
        result.Sum(kv => kv.Key.Value * kv.Value).ShouldBe(1666m);
        result[k1000].ShouldBe(1);
        result[k500].ShouldBe(1);
        result[k100].ShouldBe(1);
        result[k10].ShouldBe(6);
        result[k1].ShouldBe(6);
    }

    [Fact]
    public void CalculateShouldThrowArgumentNullExceptionWhenInventoryIsNull()
    {
        Should.Throw<ArgumentNullException>(() => ChangeCalculator.Calculate(null!, 1000));
    }

    [Fact]
    public void CalculateShouldFilterByCurrencyCode()
    {
        inv.Add(new DenominationKey(1000, CurrencyCashType.Bill, "JPY"), 1);
        inv.Add(new DenominationKey(10, CurrencyCashType.Bill, "USD"), 1);

        var result = ChangeCalculator.Calculate(inv, 1000, currencyCode: "JPY");
        result.Count.ShouldBe(1);
        result.Keys.First().CurrencyCode.ShouldBe("JPY");
    }

    [Fact]
    public void CalculateShouldPrioritizeBillsOverCoinsOfSameValue()
    {
        var kBill = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        var kCoin = new DenominationKey(1000, CurrencyCashType.Coin, "JPY");
        inv.Add(kBill, 1);
        inv.Add(kCoin, 1);

        var result = ChangeCalculator.Calculate(inv, 1000);
        result.Count.ShouldBe(1);
        result.Keys.Single().Type.ShouldBe(CurrencyCashType.Bill);
        result.ContainsKey(kCoin).ShouldBeFalse();
    }

    [Fact]
    public void CalculateShouldHandleRemainingZeroBoundary()
    {
        inv.Add(new DenominationKey(1000, CurrencyCashType.Bill, "JPY"), 1);
        inv.Add(new DenominationKey(500, CurrencyCashType.Coin, "JPY"), 1);

        var result = ChangeCalculator.Calculate(inv, 1000);
        result.Count.ShouldBe(1);
    }

    [Fact]
    public void CalculateShouldHandleNeededZero()
    {
        inv.Add(new DenominationKey(1000, CurrencyCashType.Bill, "JPY"), 1);
        inv.Add(new DenominationKey(2000, CurrencyCashType.Bill, "JPY"), 1);

        var result = ChangeCalculator.Calculate(inv, 1000);
        result.Count.ShouldBe(1);
        result.Keys.First().Value.ShouldBe(1000);
    }

    public void Dispose()
    {
        inv.Dispose();
        GC.SuppressFinalize(this);
    }
}
