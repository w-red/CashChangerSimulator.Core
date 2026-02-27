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
        new DenominationKey(0.25m, CashType.Coin, "USD")
    ];

    [Fact]
    public void Parse_ParsesStandardFormatCorrectly()
    {
        var input = "10000:2, 500:10";
        var result = CashCountParser.Parse(input, _jpyKeys).ToList();

        result.Count.ShouldBe(2);
        
        result[0].Type.ShouldBe(CashCountType.Bill);
        result[0].NominalValue.ShouldBe(10000);
        result[0].Count.ShouldBe(2);

        result[1].Type.ShouldBe(CashCountType.Coin);
        result[1].NominalValue.ShouldBe(500);
        result[1].Count.ShouldBe(10);
    }

    [Fact]
    public void Parse_ParsesExplicitFormatCorrectly()
    {
        var input = "B1000:5, C100:20";
        var result = CashCountParser.Parse(input, _jpyKeys).ToList();

        result.Count.ShouldBe(2);
        
        result[0].Type.ShouldBe(CashCountType.Bill);
        result[0].NominalValue.ShouldBe(1000);
        result[0].Count.ShouldBe(5);

        result[1].Type.ShouldBe(CashCountType.Coin);
        result[1].NominalValue.ShouldBe(100);
        result[1].Count.ShouldBe(20);
    }

    [Fact]
    public void Parse_ThrowsOnAmbiguousImplicitFormat()
    {
        // USD has both $1 Bill and $1 Coin
        var input = "1:5";
        
        var exception = Should.Throw<ArgumentException>(() => CashCountParser.Parse(input, _usdKeys));
        exception.Message.ShouldContain("Ambiguous denomination value '1'");
    }

    [Fact]
    public void Parse_ResolvesAmbiguityWithExplicitPrefix()
    {
        var input = "B1:5, C1:2";
        var result = CashCountParser.Parse(input, _usdKeys).ToList();

        result.Count.ShouldBe(2);
        
        result[0].Type.ShouldBe(CashCountType.Bill);
        result[0].NominalValue.ShouldBe(1);
        result[0].Count.ShouldBe(5);

        result[1].Type.ShouldBe(CashCountType.Coin);
        result[1].NominalValue.ShouldBe(1);
        result[1].Count.ShouldBe(2);
    }

    [Fact]
    public void Parse_ThrowsOnInvalidFormat()
    {
        Should.Throw<ArgumentException>(() => CashCountParser.Parse("1000-5", _jpyKeys));
        Should.Throw<ArgumentException>(() => CashCountParser.Parse("1000:A", _jpyKeys));
        Should.Throw<ArgumentException>(() => CashCountParser.Parse("XYZ:10", _jpyKeys));
    }

    [Fact]
    public void Parse_ThrowsOnUnsupportedDenomination()
    {
        // 2000 is not in _jpyKeys
        Should.Throw<ArgumentException>(() => CashCountParser.Parse("2000:1", _jpyKeys))
            .Message.ShouldContain("Unsupported denomination value '2000'");
    }

    [Fact]
    public void Parse_ReturnsEmptyOnEmptyInput()
    {
        CashCountParser.Parse(string.Empty, _jpyKeys).ShouldBeEmpty();
        CashCountParser.Parse("   ", _usdKeys).ShouldBeEmpty();
    }
}
