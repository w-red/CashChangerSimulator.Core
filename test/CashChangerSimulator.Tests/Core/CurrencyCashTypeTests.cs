using CashChangerSimulator.Core.Models;
using Shouldly;
using System;
using Xunit;

namespace CashChangerSimulator.Tests.Core;

/// <summary>
/// <see cref="CurrencyCashType"/> の基本動作と定義が正しいことを検証するクラス。
/// </summary>
public class CurrencyCashTypeTests
{
    /// <summary>全ての予期される値が定義されていることを確認します。</summary>
    [Fact]
    public void ShouldDefineExpectedValues()
    {
        // Assert
        Enum.GetNames(typeof(CurrencyCashType)).ShouldContain("Undefined");
        Enum.GetNames(typeof(CurrencyCashType)).ShouldContain("Coin");
        Enum.GetNames(typeof(CurrencyCashType)).ShouldContain("Bill");
    }

    /// <summary>文字列からパースできることを確認します。</summary>
    [Fact]
    public void ShouldParseFromString()
    {
        Enum.Parse<CurrencyCashType>("Coin").ShouldBe(CurrencyCashType.Coin);
        Enum.Parse<CurrencyCashType>("Bill").ShouldBe(CurrencyCashType.Bill);
        Enum.Parse<CurrencyCashType>("Undefined").ShouldBe(CurrencyCashType.Undefined);
    }
}
