using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using PosSharp.Abstractions;
using R3;
using Shouldly;

namespace CashChangerSimulator.Tests.Core.Services;

/// <summary>CurrencyMetadataProvider の動作(通貨記号、フォーマット、金種リストの取得)を検証するテストクラス。</summary>
public class CurrencyMetadataProviderTests : IDisposable
{
    private readonly ConfigurationProvider configProvider;
    private readonly CurrencyMetadataProvider provider;

    public CurrencyMetadataProviderTests()
    {
        configProvider = new ConfigurationProvider(false);
        provider = CurrencyMetadataProvider.Create(configProvider);
    }

    /// <summary>JPY 設定において、通貨記号が '¥' であることを検証します。</summary>
    [Fact]
    public void SymbolShouldReturnYenForJPY()
    {
        provider.Symbol.ShouldBe("¥");
    }

    /// <summary>USD 設定において、通貨記号が '$' であることを検証します。</summary>
    [Fact]
    public void SymbolShouldReturnDollarForUSD()
    {
        var config = new SimulatorConfiguration();
        config.System.CurrencyCode = "USD";
        configProvider.Update(config);
        provider.Symbol.ShouldBe("$");
    }

    /// <summary>JPY における金種名称のフォーマット(例：1,000 Yen Bill)が正しく生成されることを検証します。</summary>
    [Fact]
    public void GetDenominationNameShouldFormatJPYCorrectly()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        provider.GetDenominationName(key).ShouldBe("1,000 Yen Bill");
    }

    /// <summary>ja-JP カルチャが指定された場合に、日本語の金種名称(例：千円札)が返されることを検証します。</summary>
    [Fact]
    public void GetDenominationNameShouldReturnJapaneseNameForJaCulture()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        provider.GetDenominationName(key, "ja-JP").ShouldContain("千円札");
    }

    [Fact]
    public void GetDenominationNameShouldUseCustomSuffixWhenNotEmpty()
    {
        var localConfigProvider = new ConfigurationProvider(false);
        var config = new SimulatorConfiguration();
        config.System.CurrencyCode = "JPY";
        config.System.CultureCode = "ja-JP";
        config.Inventory["JPY"] = new InventorySettings
        {
            Symbol = "えん",
            Denominations = { ["B1000"] = new DenominationSettings { FormatSpecifier = "N0" } }
        };
        localConfigProvider.Update(config);

        var localProvider = CurrencyMetadataProvider.Create(localConfigProvider);
        var key = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        // Should use "えん" (custom Symbol from InventorySettings), not default "円"
        localProvider.GetDenominationName(key, "ja-JP").ShouldContain("1,000えん");
    }

    [Fact]
    public void ShouldFallbackToJpyDefaultsWhenDenominationsAreUnparseable()
    {
        var config = new SimulatorConfiguration();
        config.System.CurrencyCode = "ZZZ"; // Unknown currency
        config.Inventory.Clear();
        // Inventory entry for JPY exists but has unparseable keys
        config.Inventory["JPY"] = new InventorySettings
        {
            Symbol = "¥",
            Denominations = { ["INVALID_KEY"] = new DenominationSettings() }
        };
        configProvider.Update(config);

        provider.SupportedDenominations.ShouldNotBeEmpty();
        // Should contain default 10,000 JPY
        provider.SupportedDenominations.Any(d => d.Value == 10000).ShouldBeTrue();
        provider.Symbol.ShouldBe("¥");
    }

    [Fact]
    public void ShouldSetYenSymbolWhenCurrencyIsUnknownWithNoInventory()
    {
        var config = new SimulatorConfiguration();
        config.System.CurrencyCode = "ZZZ";
        config.Inventory.Clear(); // No inventory at all
        configProvider.Update(config);

        provider.Symbol.ShouldBe("¥");
    }

    /// <summary>USD における金種名称のフォーマット(例：$100 Bill)が正しく生成されることを検証します。</summary>
    [Fact]
    public void GetDenominationNameShouldFormatUSDCorrectly()
    {
        var key = new DenominationKey(100, CurrencyCashType.Bill, "USD");
        provider.GetDenominationName(key).ShouldBe("$100 Bill");
    }

    /// <summary>設定(Configuration)の更新に伴い、Changed イベントが発火されることを検証します。</summary>
    [Fact]
    public void ChangedShouldFireOnConfigurationUpdate()
    {
        var fired = false;
        provider.Changed.Subscribe(_ => fired = true);

        configProvider.Update(new SimulatorConfiguration());

        fired.ShouldBeTrue();
    }

    /// <summary>現在設定されている通貨(JPY)において、サポート対象の全金種キーが正しく取得されることを検証します。</summary>
    [Fact]
    public void SupportedDenominationsShouldReturnJPYKeysByDefault()
    {
        var denominations = provider.SupportedDenominations;
        denominations.ShouldContain(k => k.Value == 10000);
        denominations.ShouldContain(k => k.Value == 1);
        denominations.All(k => k.CurrencyCode == "JPY").ShouldBeTrue();
    }

    /// <summary>
    /// DenominationSettings の FormatSpecifier が null の場合に、
    /// GetDenominationName がデフォルトのフォーマット(N0/N2)へフォールバックすることを検証します(L179)。
    /// </summary>
    [Fact]
    public void GetDenominationNameShouldFallbackWhenFormatSpecifierIsNull()
    {
        // 1. カスタム通貨 "ABC" を作成し、FormatSpecifier を null に設定する
        var config = new SimulatorConfiguration();
        config.System.CurrencyCode = "ABC";
        config.Inventory["ABC"] = new InventorySettings
        {
            Symbol = "A",
            Denominations =
            {
                ["B1000"] = new DenominationSettings { DisplayName = null, FormatSpecifier = null, TypeName = "" },
                ["C0.5"] = new DenominationSettings { DisplayName = null, FormatSpecifier = null, TypeName = "" }
            }
        };
        configProvider.Update(config);

        // 2. 整数金種(Bill)の場合、"N0" (1,000) でフォーマットされるはず
        var billKey = new DenominationKey(1000, CurrencyCashType.Bill, "ABC");
        provider.GetDenominationName(billKey).ShouldBe("A1,000");

        // 3. 小数金種(Coin)の場合、"N2" (0.50) でフォーマットされるはず
        var coinKey = new DenominationKey(0.5m, CurrencyCashType.Coin, "ABC");
        provider.GetDenominationName(coinKey).ShouldBe("A0.50");
    }

    [Fact]
    public void CreateShouldReturnInstanceWithCorrectProperties()
    {
        var instance = CurrencyMetadataProvider.Create(configProvider);
        instance.ShouldNotBeNull();
        instance.CurrencyCode.ShouldBe("JPY");
        instance.Symbol.ShouldBe("¥");
        instance.SupportedDenominations.ShouldNotBeEmpty();
    }

    [Fact]
    public void GetDenominationNameShouldUseDefaultYenSuffixForJapaneseCulture()
    {
        var localConfigProvider = new ConfigurationProvider(false);
        var config = new SimulatorConfiguration();
        config.System.CurrencyCode = "JPY";
        config.System.CultureCode = "ja-JP";
        // Inventory exists but DisplayName is empty
        config.Inventory["JPY"] = new InventorySettings
        {
            Denominations = { ["B1000"] = new DenominationSettings { DisplayName = null } }
        };
        localConfigProvider.Update(config);

        var localProvider = CurrencyMetadataProvider.Create(localConfigProvider);
        var key = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        
        // JPY in ja-JP culture with no DisplayName should result in "1,000円"
        localProvider.GetDenominationName(key, "ja-JP").ShouldBe("1,000円");
    }

    [Fact]
    public void ShouldFallbackToJpyWhenCurrencyIsUnknown()
    {
        // 1. 未知の通貨 "ZZZ" を設定するが、Inventory には JPY の設定を残す
        var config = new SimulatorConfiguration();
        config.System.CurrencyCode = "ZZZ";
        config.System.CultureCode = "en-US";
        config.Inventory["JPY"] = new InventorySettings
        {
            Symbol = "¥",
            Denominations =
            {
                ["B10000"] = new DenominationSettings { DisplayName = "10,000 Yen", FormatSpecifier = "N0" }
            }
        };
        configProvider.Update(config);

        // 2. 金種リストが JPY のものにフォールバックされているか (ID 1032-1039)
        provider.SupportedDenominations.ShouldNotBeEmpty();
        provider.SupportedDenominations.Any(d => d.Value == 10000).ShouldBeTrue();

        // 3. シンボルが JPY 用の "¥" になっているか (ID 1008, 1013)
        provider.Symbol.ShouldBe("¥");

        // 4. 名称生成 (GetDenominationName は key.CurrencyCode を見るので、"JPY" を指定すれば設定がヒットする)
        var key = new DenominationKey(10000, CurrencyCashType.Bill, "JPY");
        provider.GetDenominationName(key).ShouldBe("10,000 Yen");
    }

    public void Dispose()
    {
        provider.Dispose();
        configProvider.Dispose();
        GC.SuppressFinalize(this);
    }
}
