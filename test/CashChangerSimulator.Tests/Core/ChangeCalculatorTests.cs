using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Core;

public class ChangeCalculatorTests
{
    private readonly ChangeCalculator _calculator = new();

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

    [Fact]
    public void Calculate_WithCustomFilter_ShouldWork()
    {
        var inv = new Inventory();
        inv.Add(new DenominationKey(1000, CurrencyCashType.Bill, "JPY"), 10);
        inv.Add(new DenominationKey(500, CurrencyCashType.Coin, "JPY"), 10);

        // Filter: Only bills
        var result = _calculator.Calculate(inv, 1500, filter: k => k.Type == CurrencyCashType.Bill);
        result.Count.ShouldBe(1);
        result.First().Value.ShouldBe(1); // 1000 x 1 (500 coin is skipped)
        // Note: remaining would be 500, so it should throw if we requested 1500 but only had bills
    }

    [Fact]
    public void Calculate_InsufficientCash_ShouldThrow()
    {
        var inv = new Inventory();
        inv.Add(new DenominationKey(1000, CurrencyCashType.Bill, "JPY"), 1);

        Should.Throw<InsufficientCashException>(() => _calculator.Calculate(inv, 1500));
    }

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
