using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using Shouldly;
using R3;

namespace CashChangerSimulator.Tests.Core;

public class CurrencyMetadataProviderTests
{
    private readonly ConfigurationProvider _configProvider;
    private readonly CurrencyMetadataProvider _provider;

    public CurrencyMetadataProviderTests()
    {
        _configProvider = new ConfigurationProvider();
        _provider = new CurrencyMetadataProvider(_configProvider);
    }

    [Fact]
    public void Constructor_ShouldInitializeWithDefaultJPY()
    {
        _provider.CurrencyCode.ShouldBe("JPY");
        _provider.Symbol.ShouldBe("¥");
    }

    [Fact]
    public void Refresh_ShouldHandleUnknownCurrency()
    {
        _configProvider.Config.System.CurrencyCode = "ZZZ";
        _configProvider.Update(_configProvider.Config);

        // Should fallback to JPY
        _provider.CurrencyCode.ShouldBe("ZZZ");
        _provider.SupportedDenominations.Count.ShouldBe(10); // JPY count
    }

    [Fact]
    public void Symbol_ShouldHandlePrefixAndSuffix()
    {
        // JPY with ja-JP culture
        _configProvider.Config.System.CurrencyCode = "JPY";
        _configProvider.Config.System.CultureCode = "ja-JP";
        _configProvider.Update(_configProvider.Config);
        _provider.Symbol.ShouldBe("円");

        // JPY with en-US culture
        _configProvider.Config.System.CultureCode = "en-US";
        _configProvider.Update(_configProvider.Config);
        _provider.Symbol.ShouldBe("¥");

        // USD
        _configProvider.Config.System.CurrencyCode = "USD";
        _configProvider.Update(_configProvider.Config);
        _provider.Symbol.ShouldBe("$");
    }

    [Fact]
    public void GetDenominationName_ShouldHandleVariousCulturesAndCurrencies()
    {
        var jpy1000 = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        var usd1 = new DenominationKey(1, CurrencyCashType.Bill, "USD");
        var eur2 = new DenominationKey(2, CurrencyCashType.Coin, "EUR");
        var unknown = new DenominationKey(100, CurrencyCashType.Bill, "XXX");

        // JPY ja-JP (Clear DisplayNameJP to test fallback formatting)
        _configProvider.Config.Inventory["JPY"].Denominations["B1000"].DisplayNameJP = "";
        _provider.GetDenominationName(jpy1000, "ja-JP").ShouldBe("1,000円");
        // JPY en-US
        _provider.GetDenominationName(jpy1000, "en-US").ShouldBe("1,000 Yen");

        // USD (Needs to switch provider currency first to hit the USD branches)
        _configProvider.Config.System.CurrencyCode = "USD";
        _configProvider.Update(_configProvider.Config);
        _provider.GetDenominationName(usd1, "en-US").ShouldBe("$1 Bill");
        var usdCoin = new DenominationKey(0.25m, CurrencyCashType.Coin, "USD");
        _provider.GetDenominationName(usdCoin, "en-US").ShouldBe("25¢ Coin");

        // EUR
        _configProvider.Config.System.CurrencyCode = "EUR";
        _configProvider.Update(_configProvider.Config);
        _provider.GetDenominationName(eur2, "en-US").ShouldBe("€2 Coin");
        var eurNote = new DenominationKey(50, CurrencyCashType.Bill, "EUR");
        _provider.GetDenominationName(eurNote, "en-US").ShouldBe("€50 Note");

        // Unknown
        _configProvider.Config.System.CurrencyCode = "XXX";
        _configProvider.Update(_configProvider.Config);
        _provider.GetDenominationName(unknown, "en-US").ShouldBe("100 (Bill)");
    }

    [Fact]
    public void Changed_ShouldNotifyOnUpdate()
    {
        var called = false;
        _provider.Changed.Subscribe(_ => called = true);
        
        _configProvider.Config.System.CurrencyCode = "USD";
        _configProvider.Update(_configProvider.Config);
        
        called.ShouldBeTrue();
    }

    [Fact]
    public void GetDenominationName_ShouldRespectDisplayNameJPForJPY()
    {
        // Arrange
        // (Note: _configProvider is already JPY by default in constructor)
        _configProvider.Config.System.CultureCode = "ja-JP";
        
        // 2000 Yen Bill setting
        var key = new DenominationKey(2000, CurrencyCashType.Bill, "JPY");
        _configProvider.Config.Inventory["JPY"].Denominations["B2000"].DisplayNameJP = "二千円札";
        _configProvider.Update(_configProvider.Config);
        
        // Act
        var name = _provider.GetDenominationName(key);

        // Assert
        name.ShouldBe("二千円札");
    }
}
