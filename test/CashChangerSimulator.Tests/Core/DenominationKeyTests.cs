using CashChangerSimulator.Core.Models;
using Shouldly;

namespace CashChangerSimulator.Tests.Core;

public class DenominationKeyTests
{
    [Theory]
    [InlineData("B1000", 1000, CurrencyCashType.Bill, "JPY")]
    [InlineData("C500", 500, CurrencyCashType.Coin, "JPY")]
    [InlineData("b2000", 2000, CurrencyCashType.Bill, "JPY")]
    public void TryParse_ValidString_ShouldSucceed(string input, decimal expectedValue, CurrencyCashType expectedType, string expectedCurrency)
    {
        DenominationKey.TryParse(input, out var result).ShouldBeTrue();
        result.ShouldNotBeNull();
        result.Value.ShouldBe(expectedValue);
        result.Type.ShouldBe(expectedType);
        result.CurrencyCode.ShouldBe(expectedCurrency);
    }

    [Theory]
    [InlineData("")]
    [InlineData("B")]
    [InlineData("X100")]
    [InlineData("BABC")]
    [InlineData(null)]
    public void TryParse_InvalidString_ShouldFail(string? input)
    {
        DenominationKey.TryParse(input!, out var result).ShouldBeFalse();
        result.ShouldBeNull();
    }

    [Fact]
    public void TryParse_WithCurrencyCode_ShouldSucceed()
    {
        DenominationKey.TryParse("B1", "USD", out var result).ShouldBeTrue();
        result!.CurrencyCode.ShouldBe("USD");
    }

    [Fact]
    public void PrefixChar_ShouldBeCorrect()
    {
        new DenominationKey(1, CurrencyCashType.Bill).PrefixChar.ShouldBe('B');
        new DenominationKey(1, CurrencyCashType.Coin).PrefixChar.ShouldBe('C');
    }

    [Fact]
    public void ToDenominationString_ShouldBeCorrect()
    {
        new DenominationKey(10.5m, CurrencyCashType.Coin).ToDenominationString().ShouldBe("C10.5");
    }
}
