using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using MoneyKind4Opos.Currencies.Interfaces;
using Shouldly;

namespace CashChangerSimulator.Tests.Device.Parsers;

public class CashCountParserTests
{
    private readonly List<DenominationKey> _jpyKeys =
    [
        new DenominationKey(10000, CashType.Bill, "JPY"),
        new DenominationKey(5000, CashType.Bill, "JPY"),
        new DenominationKey(1000, CashType.Bill, "JPY"),
        new DenominationKey(500, CashType.Coin, "JPY"),
        new DenominationKey(100, CashType.Coin, "JPY")
    ];

    private readonly List<DenominationKey> _usdKeys =
    [
        new DenominationKey(10, CashType.Bill, "USD"),
        new DenominationKey(5, CashType.Bill, "USD"),
        new DenominationKey(1, CashType.Bill, "USD"), // $1 Bill
        new DenominationKey(1, CashType.Coin, "USD"), // $1 Coin
        new DenominationKey(0.25m, CashType.Coin, "USD"),
        new DenominationKey(0.1m, CashType.Coin, "USD")
    ];

    [Fact]
    public void Parse_ParsesSemicolonFormatCorrectly_JPY()
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
    public void Parse_ParsesSemicolonFormatCorrectly_USD_AmbiguityResolved()
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
    public void Parse_SupportsDecimalShorthand()
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
    public void Parse_ThrowsOnAmbiguousImplicitFormatWithoutSemicolon()
    {
        // USD has both $1 Bill and $1 Coin. Without semicolon context, it's ambiguous.
        var input = "1:5";
        
        var exception = Should.Throw<ArgumentException>(() => CashCountParser.Parse(input, _usdKeys, 100));
        exception.Message.ShouldContain("Ambiguous denomination value '1'");
    }

    [Fact]
    public void Parse_ThrowsOnInvalidSectionCount()
    {
        // Three sections is not standard
        Should.Throw<ArgumentException>(() => CashCountParser.Parse("1:1;2:2;3:3", _usdKeys, 100));
    }

    [Fact]
    public void Parse_ReturnsEmptyOnEmptyInput()
    {
        CashCountParser.Parse(string.Empty, _jpyKeys, 1).ShouldBeEmpty();
        CashCountParser.Parse("   ", _usdKeys, 100).ShouldBeEmpty();
    }
}
