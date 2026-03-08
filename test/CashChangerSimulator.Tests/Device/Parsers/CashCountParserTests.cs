using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using Shouldly;

namespace CashChangerSimulator.Tests.Device.Parsers;

public class CashCountParserTests
{
    private readonly List<DenominationKey> _jpyKeys =
    [
        new DenominationKey(10000, CurrencyCashType.Bill, "JPY"),
        new DenominationKey(5000, CurrencyCashType.Bill, "JPY"),
        new DenominationKey(1000, CurrencyCashType.Bill, "JPY"),
        new DenominationKey(500, CurrencyCashType.Coin, "JPY"),
        new DenominationKey(100, CurrencyCashType.Coin, "JPY")
    ];

    private readonly List<DenominationKey> _usdKeys =
    [
        new DenominationKey(10, CurrencyCashType.Bill, "USD"),
        new DenominationKey(5, CurrencyCashType.Bill, "USD"),
        new DenominationKey(1, CurrencyCashType.Bill, "USD"), // $1 Bill
        new DenominationKey(1, CurrencyCashType.Coin, "USD"), // $1 Coin
        new DenominationKey(0.25m, CurrencyCashType.Coin, "USD"),
        new DenominationKey(0.1m, CurrencyCashType.Coin, "USD")
    ];

    [Fact]
    public void ParseParsesSemicolonFormatCorrectlyJpy()
    {
        // JPY: Coins (500, 100) before semicolon, Bills (10000) after
        var input = "500:10, 100:20 ; 10000:2";
        var result = CashCountParser.Parse(input, _jpyKeys, 1).ToList();

        result.Count.ShouldBe(3);

        // Coins
        result.ShouldContain(c => c.Type == CashCountType.Coin && c.NominalValue == 500 && c.Count == 10);
        result.ShouldContain(c => c.Type == CashCountType.Coin && c.NominalValue == 100 && c.Count == 20);

        // Bill
        result.ShouldContain(c => c.Type == CashCountType.Bill && c.NominalValue == 10000 && c.Count == 2);
    }

    [Fact]
    public void ParseParsesSemicolonFormatCorrectlyUsdAmbiguityResolved()
    {
        // USD: $1 Coin (1) before semicolon, $1 Bill (1) after
        var input = "1:5 ; 1:10";
        var result = CashCountParser.Parse(input, _usdKeys, 100).ToList();

        result.Count.ShouldBe(2);

        // Coin
        result[0].Type.ShouldBe(CashCountType.Coin);
        result[0].NominalValue.ShouldBe(100); // 1.0 * 100
        result[0].Count.ShouldBe(5);

        // Bill
        result[1].Type.ShouldBe(CashCountType.Bill);
        result[1].NominalValue.ShouldBe(100); // 1.0 * 100
        result[1].Count.ShouldBe(10);
    }

    [Fact]
    public void ParseSupportsDecimalShorthand()
    {
        // .1 = 0.1. Section 1 = Coin.
        var input = ".1:50 ; ";
        var result = CashCountParser.Parse(input, _usdKeys, 100).ToList();

        result.Count.ShouldBe(1);
        result[0].Type.ShouldBe(CashCountType.Coin);
        result[0].NominalValue.ShouldBe(10); // 0.1 * 100
        result[0].Count.ShouldBe(50);
    }

    [Fact]
    public void ParseThrowsOnAmbiguousImplicitFormatWithoutSemicolon()
    {
        // USD has both $1 Bill and $1 Coin. Without semicolon context, it's ambiguous.
        var input = "1:5";

        var exception = Should.Throw<ArgumentException>(() => CashCountParser.Parse(input, _usdKeys, 100));
        exception.Message.ShouldContain("Ambiguous denomination value '1'");
    }

    [Fact]
    public void ParseThrowsOnInvalidSectionCount()
    {
        // Three sections is not standard
        Should.Throw<ArgumentException>(() => CashCountParser.Parse("1:1;2:2;3:3", _usdKeys, 100));
    }

    [Fact]
    public void ParseThrowsOnMissingColon()
    {
        Should.Throw<ArgumentException>(() => CashCountParser.Parse("1000", _jpyKeys, 1));
    }

    [Fact]
    public void ParseThrowsOnInvalidNominalValue()
    {
        Should.Throw<ArgumentException>(() => CashCountParser.Parse("ABC:10", _jpyKeys, 1));
    }

    [Fact]
    public void ParseThrowsOnInvalidCountValue()
    {
        Should.Throw<ArgumentException>(() => CashCountParser.Parse("1000:XYZ", _jpyKeys, 1));
        Should.Throw<ArgumentException>(() => CashCountParser.Parse("1000:-5", _jpyKeys, 1));
    }

    [Fact]
    public void ParseThrowsOnUnsupportedDenomination()
    {
        Should.Throw<ArgumentException>(() => CashCountParser.Parse("123:10", _jpyKeys, 1));
    }

    [Fact]
    public void ParseThrowsOnDenominationInWrongSection()
    {
        // 10000 is a Bill in _jpyKeys, but we put it in Coin section (index 0)
        Should.Throw<ArgumentException>(() => CashCountParser.Parse("10000:1 ; 500:1", _jpyKeys, 1));
    }

    [Fact]
    public void ParseReturnsEmptyOnEmptyInput()
    {
        CashCountParser.Parse(string.Empty, _jpyKeys, 1).ShouldBeEmpty();
        CashCountParser.Parse("   ", _usdKeys, 100).ShouldBeEmpty();
    }

    [Fact]
    public void ParseHandlesExtraSpacesAndEmptySections()
    {
        // Leading/trailing spaces, multiple spaces, empty sections
        var input = "  500:10 ,  100:20  ;  10000:2  ";
        var result = CashCountParser.Parse(input, _jpyKeys, 1).ToList();
        result.Count.ShouldBe(3);

        var input2 = "; 10000:5"; // Only Bill section
        var result2 = CashCountParser.Parse(input2, _jpyKeys, 1).ToList();
        result2.Count.ShouldBe(1);
        result2[0].NominalValue.ShouldBe(10000);
        result2[0].Type.ShouldBe(CashCountType.Bill);

        var input3 = "500:5 ; "; // Only Coin section
        var result3 = CashCountParser.Parse(input3, _jpyKeys, 1).ToList();
        result3.Count.ShouldBe(1);
        result3[0].NominalValue.ShouldBe(500);
        result3[0].Type.ShouldBe(CashCountType.Coin);
    }

    [Fact]
    public void ParseAllowsZeroCount()
    {
        var input = "500:0;1000:0";
        var result = CashCountParser.Parse(input, _jpyKeys, 1).ToList();
        result.Count.ShouldBe(2);
        result.All(c => c.Count == 0).ShouldBeTrue();
    }

    [Fact]
    public void ParseThrowsOnInvalidPartFormat()
    {
        // Missing value or count
        Should.Throw<ArgumentException>(() => CashCountParser.Parse("100:", _jpyKeys, 1));
        Should.Throw<ArgumentException>(() => CashCountParser.Parse(":5", _jpyKeys, 1));
        Should.Throw<ArgumentException>(() => CashCountParser.Parse("100:5:10", _jpyKeys, 1));
    }
}
