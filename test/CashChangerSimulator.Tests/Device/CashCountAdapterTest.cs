using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using MoneyKind4Opos.Currencies.Interfaces;

namespace CashChangerSimulator.Tests.Device;

/// <summary>CashCountAdapter の変換ロジックを検証するテストクラス。</summary>
public class CashCountAdapterTest
{
    /// <summary>CashCount から DenominationKey への変換が正しいことを確認します。</summary>
    [Fact]
    public void ToDenominationKey_ShouldConvertCorrectly()
    {
        var cc = new CashCount(CashCountType.Bill, 1000, 5);
        var key = CashCountAdapter.ToDenominationKey(cc, "JPY", 1m);

        Assert.Equal(1000m, key.Value);
        Assert.Equal(CashType.Bill, key.Type);
        Assert.Equal("JPY", key.CurrencyCode);
    }

    /// <summary>Coin タイプの CashCount 変換が正しいことを確認します。</summary>
    [Fact]
    public void ToDenominationKey_Coin_ShouldConvertCorrectly()
    {
        var cc = new CashCount(CashCountType.Coin, 500, 10);
        var key = CashCountAdapter.ToDenominationKey(cc, "JPY", 1m);

        Assert.Equal(500m, key.Value);
        Assert.Equal(CashType.Coin, key.Type);
    }

    /// <summary>通貨係数を適用した変換が正しいことを確認します (USD: factor=100)。</summary>
    [Fact]
    public void ToDenominationKey_WithCurrencyFactor_ShouldApplyFactor()
    {
        var cc = new CashCount(CashCountType.Bill, 500, 2); // $5.00 = 500 cents
        var key = CashCountAdapter.ToDenominationKey(cc, "USD", 100m);

        Assert.Equal(5m, key.Value);
        Assert.Equal(CashType.Bill, key.Type);
        Assert.Equal("USD", key.CurrencyCode);
    }

    /// <summary>DenominationKey から CashCount への逆変換が正しいことを確認します。</summary>
    [Fact]
    public void ToCashCount_ShouldConvertCorrectly()
    {
        var key = new DenominationKey(1000m, CashType.Bill, "JPY");
        var cc = CashCountAdapter.ToCashCount(key, 5, 1m);

        Assert.Equal(CashCountType.Bill, cc.Type);
        Assert.Equal(1000, cc.NominalValue);
        Assert.Equal(5, cc.Count);
    }

    /// <summary>CashCount 配列から Dictionary への一括変換が正しいことを確認します。</summary>
    [Fact]
    public void ToDenominationDict_ShouldConvertArray()
    {
        var cashCounts = new[]
        {
            new CashCount(CashCountType.Bill, 1000, 3),
            new CashCount(CashCountType.Coin, 100, 5),
        };

        var dict = CashCountAdapter.ToDenominationDict(cashCounts, "JPY", 1m);

        Assert.Equal(2, dict.Count);
        Assert.Equal(3, dict[new DenominationKey(1000m, CashType.Bill, "JPY")]);
        Assert.Equal(5, dict[new DenominationKey(100m, CashType.Coin, "JPY")]);
    }

    /// <summary>負の Count を持つ CashCount で例外がスローされることを確認します。</summary>
    [Fact]
    public void ToDenominationDict_NegativeCount_ShouldThrow()
    {
        var cashCounts = new[] { new CashCount(CashCountType.Bill, 1000, -1) };

        Assert.Throws<PosControlException>(() =>
            CashCountAdapter.ToDenominationDict(cashCounts, "JPY", 1m));
    }
}
