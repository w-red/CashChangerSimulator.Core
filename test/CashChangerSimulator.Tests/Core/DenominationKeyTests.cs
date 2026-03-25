using CashChangerSimulator.Core.Models;
using Shouldly;

namespace CashChangerSimulator.Tests.Core;

/// <summary>金種キー（DenominationKey）のパース、文字列表現、プロパティを検証するテストクラス。</summary>
public class DenominationKeyTests
{
    /// <summary>有効な文字列（B1000等）から金種キーが正しくパースされることを検証します。</summary>
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

    /// <summary>不正な形式や null 文字列を指定した際、パースが失敗することを検証します。</summary>
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

    /// <summary>通貨コードを指定したパースが正しく行われることを検証します。</summary>
    [Fact]
    public void TryParse_WithCurrencyCode_ShouldSucceed()
    {
        DenominationKey.TryParse("B1", "USD", out var result).ShouldBeTrue();
        result!.CurrencyCode.ShouldBe("USD");
    }

    /// <summary>金種タイプに応じた接頭辞（B/C）が正しく返されることを検証します。</summary>
    [Fact]
    public void PrefixChar_ShouldBeCorrect()
    {
        new DenominationKey(1, CurrencyCashType.Bill).PrefixChar.ShouldBe('B');
        new DenominationKey(1, CurrencyCashType.Coin).PrefixChar.ShouldBe('C');
    }

    /// <summary>金種キーの文字列表現（例：C10.5）が正しく出力されることを検証します。</summary>
    [Fact]
    public void ToDenominationString_ShouldBeCorrect()
    {
        new DenominationKey(10.5m, CurrencyCashType.Coin).ToDenominationString().ShouldBe("C10.5");
    }
}
