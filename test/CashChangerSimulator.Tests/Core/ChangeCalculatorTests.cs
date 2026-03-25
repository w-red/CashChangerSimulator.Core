using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Core;

/// <summary>出金計算ロジック（最適な金種構成の算出、通貨フィルタ等）を検証するテストクラス。</summary>
public class ChangeCalculatorTests
{
    private readonly ChangeCalculator _calculator = new();

    /// <summary>通貨コード指定のフィルタが正しく機能し、指定通貨のみで出金計算が行われることを検証します。</summary>
    [Fact]
    public void Calculate_WithCurrencyFilter_ShouldWork()
    {
        var inv = new Inventory();
        inv.Add(new DenominationKey(1000, CurrencyCashType.Bill, "JPY"), 10);
        inv.Add(new DenominationKey(1, CurrencyCashType.Bill, "USD"), 10);

        var result = _calculator.Calculate(inv, 1000, "JPY");
        result.Count.ShouldBe(1);
        result.Keys.First().CurrencyCode.ShouldBe("JPY");
    }

    /// <summary>カスタムフィルタ（例：紙幣のみ）が指定された際に、条件に合う金種のみで計算されることを検証します。</summary>
    [Fact]
    public void Calculate_WithCustomFilter_ShouldWork()
    {
        var inv = new Inventory();
        inv.Add(new DenominationKey(1000, CurrencyCashType.Bill, "JPY"), 10);
        inv.Add(new DenominationKey(500, CurrencyCashType.Coin, "JPY"), 10);

        // Filter: Only bills. 1500 cannot be paid fully with only 1000 bills.
        Should.Throw<InsufficientCashException>(() => _calculator.Calculate(inv, 1500, filter: k => k.Type == CurrencyCashType.Bill));
    }

    /// <summary>在庫不足により出金計算が不可能な場合に InsufficientCashException がスローされることを検証します。</summary>
    [Fact]
    public void Calculate_InsufficientCash_ShouldThrow()
    {
        var inv = new Inventory();
        inv.Add(new DenominationKey(1000, CurrencyCashType.Bill, "JPY"), 1);

        Should.Throw<InsufficientCashException>(() => _calculator.Calculate(inv, 1500));
    }

    /// <summary>Inventory クラス以外の IReadOnlyInventory 実装を渡した際のエラーハンドリングを検証します。</summary>
    [Fact]
    public void Calculate_WithNonInventoryType_ShouldReturnEmpty()
    {
        // GetAvailableDenominationKeys uses 'inventory is Inventory'
        // If we pass a mock IReadOnlyInventory, it should return empty list of keys
        var mockInv = new Mock<IReadOnlyInventory>();
        mockInv.Setup(m => m.GetCount(It.IsAny<DenominationKey>())).Returns(10);

        Should.Throw<InsufficientCashException>(() => _calculator.Calculate(mockInv.Object, 100));
    }
}
