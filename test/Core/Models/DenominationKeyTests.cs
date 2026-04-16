using CashChangerSimulator.Core.Models;
using Shouldly;

namespace CashChangerSimulator.Tests.Core.Models;

/// <summary>金種キー(DenominationKey)のパース、文字列表現、プロパティを検証するテストクラス。</summary>
public class DenominationKeyTests
{
    /// <summary>有効な文字列(B1000等)から金種キーが正しくパースされることを検証します。</summary>
    [Theory]
    [InlineData("B1000", 1000, CurrencyCashType.Bill, "JPY")]
    [InlineData("C500", 500, CurrencyCashType.Coin, "JPY")]
    [InlineData("b2000", 2000, CurrencyCashType.Bill, "JPY")]
    [InlineData("JPY:B1000", 1000, CurrencyCashType.Bill, "JPY")]
    [InlineData("USD:C0.25", 0.25, CurrencyCashType.Coin, "USD")]
    [InlineData("c1", 1, CurrencyCashType.Coin, "JPY")]
    public void TryParseValidStringShouldSucceed(string input, decimal expectedValue, CurrencyCashType expectedType, string expectedCurrency)
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
    [InlineData("C")]
    [InlineData("X100")]
    [InlineData("BABC")]
    [InlineData(":B1000")] // 通貨コード空
    [InlineData("JPY:")] // 金種文字列空
    [InlineData("JPY:B")] // 金種文字列短すぎ
    [InlineData("JPY: B1000")] // 接頭辞の前にスペース
    [InlineData(null)]
    public void TryParseInvalidStringShouldFail(string? input)
    {
        DenominationKey.TryParse(input!, out var result).ShouldBeFalse();
        result.ShouldBeNull();
    }

    /// <summary>通貨コードを指定したパースが正しく行われることを検証します。</summary>
    [Fact]
    public void TryParseWithCurrencyCodeShouldSucceed()
    {
        DenominationKey.TryParse("B1", "USD", out var result).ShouldBeTrue();
        result!.CurrencyCode.ShouldBe("USD");
    }

    /// <summary>金種タイプに応じた接頭辞(B/C)が正しく返されることを検証します。</summary>
    [Fact]
    public void PrefixCharShouldBeCorrect()
    {
        new DenominationKey(1, CurrencyCashType.Bill).PrefixChar.ShouldBe('B');
        new DenominationKey(1, CurrencyCashType.Coin).PrefixChar.ShouldBe('C');
    }

    /// <summary>金種キーの文字列表現(例：C10.5)が正しく出力されることを検証します。</summary>
    [Fact]
    public void ToDenominationStringShouldBeCorrect()
    {
        new DenominationKey(10.5m, CurrencyCashType.Coin).ToDenominationString().ShouldBe("C10.5");
    }

    /// <summary>等価性比較が正しく動作することを検証します。</summary>
    [Fact]
    public void EqualsShouldWorkCorrectly()
    {
        var key1 = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        var key2 = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        var key3 = new DenominationKey(2000, CurrencyCashType.Bill, "JPY");
        var key4 = new DenominationKey(1000, CurrencyCashType.Coin, "JPY");
        var key5 = new DenominationKey(1000, CurrencyCashType.Bill, "USD");

        // 基本的な比較
        key1.Equals(key2).ShouldBeTrue();
        (key1 == key2).ShouldBeTrue();

        // 各プロパティの不一致
        key1.Equals(key3).ShouldBeFalse();
        key1.Equals(key4).ShouldBeFalse();
        key1.Equals(key5).ShouldBeFalse();

        // null との比較
        key1.Equals(null!).ShouldBeFalse();

        // 異なる型との比較
#pragma warning disable CS1803
        key1.Equals("not a key").ShouldBeFalse();
#pragma warning restore CS1803

        // 同一参照
#pragma warning disable PS0019 // Use ReferenceEquals for identity comparison
        key1.Equals(key1).ShouldBeTrue();
#pragma warning restore PS0019
    }

    /// <summary>ハッシュ値が一貫していることを検証します。</summary>
    [Fact]
    public void GetHashCodeShouldBeConsistent()
    {
        var key1 = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        var key2 = new DenominationKey(1000.00m, CurrencyCashType.Bill, "JPY");
        var key3 = new DenominationKey(2000, CurrencyCashType.Bill, "JPY");

        // 同値ならハッシュ値も同じ
        key1.GetHashCode().ShouldBe(key2.GetHashCode());

        // 異なればハッシュ値も（高確率で）異なる
        key1.GetHashCode().ShouldNotBe(key3.GetHashCode());

        // 文字列変換(G29)によって精度が異なっても同じハッシュ値になることを確認
        var key4 = new DenominationKey(100.1m, CurrencyCashType.Coin, "JPY");
        var key5 = new DenominationKey(100.100m, CurrencyCashType.Coin, "JPY");
        key4.GetHashCode().ShouldBe(key5.GetHashCode());
    }

    /// <summary>TryParse において、通貨コードに null を指定した場合に ArgumentNullException がスローされることを検証します。</summary>
    [Fact]
    public void TryParse_ShouldThrowArgumentNullException_WhenCurrencyCodeIsNull()
    {
        Should.Throw<ArgumentNullException>(() => DenominationKey.TryParse("B1000", null!, out _));
    }

    /// <summary>セパレータが複数含まれる場合、最初のセパレータで分割されることを検証します。</summary>
    [Fact]
    public void TryParse_ShouldHandleMultipleSeparators()
    {
        // "USD:JPY:B1000" -> targetCurrency="USD", targetDenomination="JPY:B1000"
        // targetDenomination[0] is 'J', which is Undefined. Output should be false.
        DenominationKey.TryParse("USD:JPY:B1000", out _).ShouldBeFalse();
    }
}
