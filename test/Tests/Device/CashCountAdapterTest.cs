using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>CashCountAdapter の変換ロジックを検証するテストクラス。</summary>
public class CashCountAdapterTest
{
    /// <summary>CashCount から DenominationKey への変換が正しいことを確認します。</summary>
    [Fact]
    public void ToDenominationKeyShouldConvertCorrectly()
    {
        var cc = new CashCount(CashCountType.Bill, 1000, 5);
        var key = CashCountAdapter.ToDenominationKey(cc, "JPY", 1m);

        key.Value.ShouldBe(1000m);
        key.Type.ShouldBe(CurrencyCashType.Bill);
        key.CurrencyCode.ShouldBe("JPY");
    }

    /// <summary>Coin タイプの CashCount 変換が正しいことを確認します。</summary>
    [Fact]
    public void ToDenominationKeyCoinShouldConvertCorrectly()
    {
        var cc = new CashCount(CashCountType.Coin, 500, 10);
        var key = CashCountAdapter.ToDenominationKey(cc, "JPY", 1m);

        key.Value.ShouldBe(500m);
        key.Type.ShouldBe(CurrencyCashType.Coin);
        key.CurrencyCode.ShouldBe("JPY");
    }

    /// <summary>通貨係数を適用した変換が正しいことを確認します (USD: factor=100)。</summary>
    [Fact]
    public void ToDenominationKeyWithCurrencyFactorShouldApplyFactor()
    {
        var cc = new CashCount(CashCountType.Bill, 500, 2); // $5.00 = 500 cents
        var key = CashCountAdapter.ToDenominationKey(cc, "USD", 100m);

        key.Value.ShouldBe(5m);
        key.Type.ShouldBe(CurrencyCashType.Bill);
        key.CurrencyCode.ShouldBe("USD");
    }

    /// <summary>DenominationKey から CashCount への逆変換が正しいことを確認します。</summary>
    [Fact]
    public void ToCashCountShouldConvertCorrectly()
    {
        var key = new DenominationKey(1000m, CurrencyCashType.Bill, "JPY");
        var cc = CashCountAdapter.ToCashCount(key, 5, 1m);

        cc.Type.ShouldBe(CashCountType.Bill);
        cc.NominalValue.ShouldBe(1000);
        cc.Count.ShouldBe(5);
    }

    /// <summary>CashCount 配列から Dictionary への一括変換が正しいことを確認します。</summary>
    [Fact]
    public void ToDenominationDictShouldConvertArray()
    {
        var cashCounts = new[]
        {
            new CashCount(CashCountType.Bill, 1000, 3),
            new CashCount(CashCountType.Coin, 100, 5),
        };

        var dict = CashCountAdapter.ToDenominationDict(cashCounts, "JPY", 1m);

        dict.Count.ShouldBe(2);
        dict[new DenominationKey(1000m, CurrencyCashType.Bill, "JPY")].ShouldBe(3);
        dict[new DenominationKey(100m, CurrencyCashType.Coin, "JPY")].ShouldBe(5);
    }

    /// <summary>負の Count を持つ CashCount で例外がスローされることを確認します。</summary>
    [Fact]
    public void ToDenominationDictNegativeCountShouldThrow()
    {
        var cashCounts = new[] { new CashCount(CashCountType.Bill, 1000, -1) };

        Should.Throw<PosControlException>(() =>
            CashCountAdapter.ToDenominationDict(cashCounts, "JPY", 1m));
    }
}
