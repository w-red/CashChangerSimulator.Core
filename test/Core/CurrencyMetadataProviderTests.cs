using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using R3;
using Shouldly;

namespace CashChangerSimulator.Tests.Core;

/// <summary>通貨メタデータ（記号、名称、サポート金種等）の提供機能を検証するテストクラス。</summary>
public class CurrencyMetadataProviderTests
{
    private readonly ConfigurationProvider configProvider;
    private readonly CurrencyMetadataProvider provider;

    public CurrencyMetadataProviderTests()
    {
        configProvider = new ConfigurationProvider();
        provider = new CurrencyMetadataProvider(configProvider);
    }

    /// <summary>コンストラクタ実行時にデフォルトの通貨（JPY）で初期化されることを検証します。</summary>
    [Fact]
    public void ConstructorShouldInitializeWithDefaultJpy()
    {
        provider.CurrencyCode.ShouldBe("JPY");
        provider.Symbol.ShouldBe("¥");
    }

    /// <summary>未知の通貨コードが設定された際、適切にデフォルト（JPY）へフォールバックされることを検証します。</summary>
    [Fact]
    public void RefreshShouldHandleUnknownCurrency()
    {
        configProvider.Config.System.CurrencyCode = "ZZZ";
        configProvider.Update(configProvider.Config);

        // Should fallback to JPY
        provider.CurrencyCode.ShouldBe("ZZZ");
        provider.SupportedDenominations.Count.ShouldBe(10); // JPY count
    }

    /// <summary>通貨記号が文化圏（Culture）や設定に応じて正しく取得されることを検証します。</summary>
    [Fact]
    public void SymbolShouldHandlePrefixAndSuffix()
    {
        // JPY with ja-JP culture
        configProvider.Config.System.CurrencyCode = "JPY";
        configProvider.Config.System.CultureCode = "ja-JP";
        configProvider.Update(configProvider.Config);
        provider.Symbol.ShouldBe("円");

        // JPY with en-US culture
        configProvider.Config.System.CultureCode = "en-US";
        configProvider.Update(configProvider.Config);
        provider.Symbol.ShouldBe("¥");

        // USD
        configProvider.Config.System.CurrencyCode = "USD";
        configProvider.Update(configProvider.Config);
        provider.Symbol.ShouldBe("$");
    }

    /// <summary>様々な文化圏や通貨において、金種名称が正しく生成されることを検証します。</summary>
    [Fact]
    public void GetDenominationNameShouldHandleVariousCulturesAndCurrencies()
    {
        var jpy1000 = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        var usd1 = new DenominationKey(1, CurrencyCashType.Bill, "USD");
        var eur2 = new DenominationKey(2, CurrencyCashType.Coin, "EUR");
        var unknown = new DenominationKey(100, CurrencyCashType.Bill, "XXX");

        // JPY ja-JP (Clear display names to test fallback formatting)
        configProvider.Config.Inventory["JPY"].Denominations["B1000"].DisplayNameJP = string.Empty;
        configProvider.Config.Inventory["JPY"].Denominations["B1000"].DisplayName = string.Empty;
        provider.GetDenominationName(jpy1000, "ja-JP").ShouldBe("1,000円");

        // JPY en-US
        provider.GetDenominationName(jpy1000, "en-US").ShouldBe("1,000 Yen");

        // USD (Needs to switch provider currency first to hit the USD branches)
        configProvider.Config.System.CurrencyCode = "USD";
        configProvider.Update(configProvider.Config);
        provider.GetDenominationName(usd1, "en-US").ShouldBe("$1 Bill");
        var usdCoin = new DenominationKey(0.25m, CurrencyCashType.Coin, "USD");
        provider.GetDenominationName(usdCoin, "en-US").ShouldBe("25¢ Coin");

        // EUR
        configProvider.Config.System.CurrencyCode = "EUR";
        configProvider.Update(configProvider.Config);
        provider.GetDenominationName(eur2, "en-US").ShouldBe("€2 Coin");
        var eurNote = new DenominationKey(50, CurrencyCashType.Bill, "EUR");
        provider.GetDenominationName(eurNote, "en-US").ShouldBe("€50 Note");

        // Unknown
        configProvider.Config.System.CurrencyCode = "XXX";
        configProvider.Update(configProvider.Config);
        provider.GetDenominationName(unknown, "en-US").ShouldBe("100 (Bill)");
    }

    /// <summary>設定変更時に通知イベント（Changed）が正しく発火されることを検証します。</summary>
    [Fact]
    public void ChangedShouldNotifyOnUpdate()
    {
        var called = false;
        provider.Changed.Subscribe(_ => called = true);

        configProvider.Config.System.CurrencyCode = "USD";
        configProvider.Update(configProvider.Config);

        called.ShouldBeTrue();
    }

    /// <summary>日本円（JPY）において、設定ファイルの DisplayNameJP が優先的に使用されることを検証します。</summary>
    [Fact]
    public void GetDenominationNameShouldRespectDisplayNameJpForJpy()
    {
        // Arrange
        // (Note: _configProvider is already JPY by default in constructor)
        configProvider.Config.System.CultureCode = "ja-JP";

        // 2000 Yen Bill setting
        var key = new DenominationKey(2000, CurrencyCashType.Bill, "JPY");
        configProvider.Config.Inventory["JPY"].Denominations["B2000"].DisplayNameJP = "二千円札";
        configProvider.Update(configProvider.Config);

        // Act
        var name = provider.GetDenominationName(key);

        // Assert
        name.ShouldBe("二千円札");
    }

    /// <summary>SymbolPrefix と SymbolSuffix が ReadOnlyReactiveProperty を通じて正しく取得できることを検証します。</summary>
    [Fact]
    public void SymbolPrefixAndSuffixShouldWork()
    {
        // JPY en-US -> ¥ as prefix
        configProvider.Config.System.CurrencyCode = "JPY";
        configProvider.Config.System.CultureCode = "en-US";
        configProvider.Update(configProvider.Config);

        provider.SymbolPrefix.CurrentValue.ShouldBe("¥");
        provider.SymbolSuffix.CurrentValue.ShouldBe(string.Empty);

        // JPY ja-JP -> 円 as suffix
        configProvider.Config.System.CultureCode = "ja-JP";
        configProvider.Update(configProvider.Config);

        provider.SymbolPrefix.CurrentValue.ShouldBe(string.Empty);
        provider.SymbolSuffix.CurrentValue.ShouldBe("円");
    }

    /// <summary>GetDenominationName の未カバーの条件分岐（非日本語環境での DisplayName 等）を検証します。</summary>
    [Fact]
    public void GetDenominationNameExtraPaths()
    {
        // 1. JPY, en-US, with DisplayName set
        configProvider.Config.System.CurrencyCode = "JPY";
        configProvider.Config.System.CultureCode = "en-US";
        configProvider.Config.Inventory["JPY"].Denominations["B10000"].DisplayName = "Ten Thousand Yen";
        configProvider.Update(configProvider.Config);

        var key = new DenominationKey(10000, CurrencyCashType.Bill, "JPY");
        provider.GetDenominationName(key).ShouldBe("Ten Thousand Yen");

        // 2. Unknown currency (XXX), ja-JP culture, with DisplayNameJP set
        configProvider.Config.System.CurrencyCode = "XXX";
        configProvider.Config.System.CultureCode = "ja-JP";
        configProvider.Update(configProvider.Config);

        // Note: XXX doesn't exist in config by default, but GetDenominationSetting returns a default if missing?
        // Let's assume we can add it to inventory if needed, or it fallbacks to "100 (Bill)"-like format.
        var unknownKey = new DenominationKey(100, CurrencyCashType.Bill, "XXX");
        provider.GetDenominationName(unknownKey).ShouldBe("100 (Bill)");
    }

    /// <summary>Dispose メソッドがリソースを適切に解放し、複数回呼び出しが可能であることを検証します。</summary>
    [Fact]
    public void DisposeShouldWork()
    {
        // Act & Assert
        Should.NotThrow(() =>
        {
            provider.Dispose();
            provider.Dispose();
        });
    }
}
