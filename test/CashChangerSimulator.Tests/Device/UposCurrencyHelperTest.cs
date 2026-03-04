using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;

namespace CashChangerSimulator.Tests.Device;

/// <summary>UposCurrencyHelper の動作を検証するテストクラス。</summary>
public class UposCurrencyHelperTest
{
    /// <summary>指定された通貨コードに対する係数が正しく取得できるかを確認します。</summary>
    [Theory]
    [InlineData("USD", 100)]
    [InlineData("EUR", 100)]
    [InlineData("GBP", 100)]
    [InlineData("JPY", 1)]
    [InlineData("UNKNOWN", 1)]
    public void GetCurrencyFactor_ShouldReturnCorrectFactor(string currencyCode, decimal expectedFactor)
    {
        var factor = UposCurrencyHelper.GetCurrencyFactor(currencyCode);
        Assert.Equal(expectedFactor, factor);
    }

    /// <summary>DenominationKey から NominalValue が正しく計算されるかを確認します。</summary>
    [Fact]
    public void GetNominalValue_ShouldCalculateCorrectly()
    {
        var keyJpy = new DenominationKey(1000m, CurrencyCashType.Bill, "JPY");
        var valJpy = UposCurrencyHelper.GetNominalValue(keyJpy);
        Assert.Equal(1000, valJpy);

        var keyUsd = new DenominationKey(5m, CurrencyCashType.Bill, "USD");
        var valUsd = UposCurrencyHelper.GetNominalValue(keyUsd);
        Assert.Equal(500, valUsd); // 5 * 100
    }

    /// <summary>在庫から指定した通貨の CashUnits が正しく生成されるかを確認します。</summary>
    [Fact]
    public void BuildCashUnits_ShouldBuildCorrectly()
    {
        var inventory = new Inventory();
        inventory.SetCount(new DenominationKey(1000m, CurrencyCashType.Bill, "JPY"), 1);
        inventory.SetCount(new DenominationKey(500m, CurrencyCashType.Coin, "JPY"), 1);
        inventory.SetCount(new DenominationKey(10m, CurrencyCashType.Bill, "USD"), 1); // Should be ignored since active currency is JPY

        var cashUnits = UposCurrencyHelper.BuildCashUnits(inventory, "JPY");

        Assert.Single(cashUnits.Coins);
        Assert.Equal(500, cashUnits.Coins.First());

        Assert.Single(cashUnits.Bills);
        Assert.Equal(1000, cashUnits.Bills.First());
    }
}
