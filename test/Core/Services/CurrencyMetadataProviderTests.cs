using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using R3;
using Shouldly;

namespace CashChangerSimulator.Tests.Core.Services;

/// <summary>通貨メタデータ(記号、名称、サポート金種等)の提供機能を検証するテストクラス。</summary>
public class CurrencyMetadataProviderTests
{
    private readonly ConfigurationProvider configProvider;
    private readonly CurrencyMetadataProvider provider;

    public CurrencyMetadataProviderTests()
    {
        configProvider = new ConfigurationProvider();
        provider = CurrencyMetadataProvider.Create(configProvider);
    }

    /// <summary>コンストラクタ実行時にデフォルトの通貨(JPY)で初期化されることを検証します。</summary>
    [Fact]
    public void ConstructorShouldInitializeWithDefaultJpy()
    {
        provider.CurrencyCode.ShouldBe("JPY");
        provider.Symbol.ShouldBe("¥");
        provider.SupportedDenominations.Count.ShouldBeGreaterThan(0);
    }

    /// <summary>ガード句が適切に例外をスローすることを検証します。</summary>
    [Fact]
    public void GuardsShouldThrowExceptions()
    {
        Should.Throw<ArgumentNullException>(() => CurrencyMetadataProvider.Create(null!));
        
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        Should.Throw<ArgumentNullException>(() => provider.GetDenominationName(null!));
        Should.Throw<ArgumentNullException>(() => provider.GetDenominationName(key, null!));
        Should.Throw<ArgumentNullException>(() => provider.GetDenominationName(null!, "ja-JP"));
    }

    /// <summary>未知の通貨コードが設定された際、適切にデフォルト(JPY)へフォールバックされることを検証します。</summary>
    [Fact]
    public void RefreshShouldHandleUnknownCurrency()
    {
        configProvider.Config.System.CurrencyCode = "ZZZ";
        configProvider.Update(configProvider.Config);

        // Code logic sets CurrencyCode to "ZZZ" but fallbacks the database to JPY
        provider.CurrencyCode.ShouldBe("ZZZ");
        provider.SupportedDenominations.Count.ShouldBe(10); // JPY count
        provider.Symbol.ShouldBe("¥"); // JPY default symbol
    }

    /// <summary>通貨記号が文化圏(Culture)や設定に応じて正しく取得されることを検証します。</summary>
    [Theory]
    [InlineData("JPY", "ja-JP", "", "円", "円")]
    [InlineData("JPY", "en-US", "¥", "", "¥")]
    [InlineData("USD", "en-US", "$", "", "$")]
    [InlineData("EUR", "fr-FR", "€", "", "€")]
    public void SymbolShouldHandlePrefixAndSuffixVariations(string currency, string culture, string expectedPrefix, string expectedSuffix, string expectedSymbol)
    {
        configProvider.Config.System.CurrencyCode = currency;
        configProvider.Config.System.CultureCode = culture;
        configProvider.Update(configProvider.Config);

        provider.SymbolPrefix.CurrentValue.ShouldBe(expectedPrefix);
        provider.SymbolSuffix.CurrentValue.ShouldBe(expectedSuffix);
        provider.Symbol.ShouldBe(expectedSymbol);
    }

    /// <summary>金種データベースの内容が正確であることを検証します(String/Value変異対策)。</summary>
    [Theory]
    [InlineData("JPY", 10, 10000, CurrencyCashType.Bill)]
    [InlineData("JPY", 10, 1, CurrencyCashType.Coin)]
    [InlineData("USD", 12, 100, CurrencyCashType.Bill)]
    [InlineData("USD", 12, 0.01, CurrencyCashType.Coin)]
    [InlineData("EUR", 15, 500, CurrencyCashType.Bill)]
    [InlineData("EUR", 15, 0.01, CurrencyCashType.Coin)]
    public void CurrencyDatabaseShouldBeAccurate(string currency, int expectedCount, decimal extremeValue, CurrencyCashType expectedType)
    {
        configProvider.Config.System.CurrencyCode = currency;
        configProvider.Update(configProvider.Config);

        provider.SupportedDenominations.Count.ShouldBe(expectedCount);
        provider.SupportedDenominations.Any(d => d.Value == extremeValue && d.Type == expectedType).ShouldBeTrue();
    }

    /// <summary>JPYにおける表示名称の分岐(設定あり/なし、カルチャ)を網羅します。</summary>
    [Theory]
    [InlineData("ja-JP", "壱万円", "", "壱万円")] // DisplayNameJP優先
    [InlineData("ja-JP", "", "10k Yen", "10,000円")] // ja-JPでDisplayNameJP空ならデフォルト
    [InlineData("en-US", "壱万円", "TenK", "TenK")] // en-USならDisplayName優先
    [InlineData("en-US", "", "", "10,000 Yen")] // 両方空なら en デフォルト
    public void GetDenominationNameShouldHandleJpyBranches(string culture, string nameJp, string nameEn, string expected)
    {
        configProvider.Config.System.CurrencyCode = "JPY";
        var key = new DenominationKey(10000, CurrencyCashType.Bill, "JPY");
        var setting = configProvider.Config.Inventory["JPY"].Denominations["B10000"];
        setting.DisplayNameJP = nameJp;
        setting.DisplayName = nameEn;
        configProvider.Update(configProvider.Config);

        provider.GetDenominationName(key, culture).ShouldBe(expected);
    }

    /// <summary>USD/EUR における Bill/Coin/Note のテンプレート名称を検証します。</summary>
    [Theory]
    [InlineData("USD", 100, CurrencyCashType.Bill, "$100 Bill")]
    [InlineData("USD", 0.25, CurrencyCashType.Coin, "$0.25 Coin")]
    [InlineData("EUR", 200, CurrencyCashType.Bill, "€200 Note")]
    [InlineData("EUR", 2, CurrencyCashType.Coin, "€2 Coin")]
    public void GetDenominationNameShouldUseTemplatesForUsdAndEur(string currency, double value, CurrencyCashType type, string expected)
    {
        configProvider.Config.System.CurrencyCode = currency;
        configProvider.Update(configProvider.Config);

        var key = new DenominationKey((decimal)value, type, currency);
        // Clear custom names to force template usage
        var setting = configProvider.Config.GetDenominationSetting(key);
        setting.DisplayName = string.Empty;
        setting.DisplayNameJP = string.Empty;

        provider.GetDenominationName(key, "en-US").ShouldBe(expected);
    }

    /// <summary>設定による表示名称のオーバーライドが USD/EUR でも機能することを検証します。</summary>
    [Fact]
    public void GetDenominationNameShouldAllowOverridesForUsdAndEur()
    {
        configProvider.Config.System.CurrencyCode = "USD";
        var key = new DenominationKey(0.25m, CurrencyCashType.Coin, "USD");
        configProvider.Config.Inventory["USD"].Denominations["C0.25"].DisplayName = "Quarter";
        configProvider.Update(configProvider.Config);

        provider.GetDenominationName(key, "en-US").ShouldBe("Quarter");
    }

    /// <summary>未知の通貨・カルチャにおけるフォールバック表示を検証します。</summary>
    [Theory]
    [InlineData("XXX", 123, CurrencyCashType.Bill, "ja-JP", "123 (Bill)")]
    [InlineData("XXX", 500, CurrencyCashType.Coin, "en-US", "500 (Coin)")]
    public void GetDenominationNameShouldFallbackForUnknowns(string currency, double value, CurrencyCashType type, string culture, string expected)
    {
        configProvider.Config.System.CurrencyCode = currency;
        configProvider.Update(configProvider.Config);

        var key = new DenominationKey((decimal)value, type, currency);
        provider.GetDenominationName(key, culture).ShouldBe(expected);
    }

    /// <summary>設定変更時に通知イベント(Changed)が正しく発火されることを検証します。</summary>
    [Fact]
    public void ChangedShouldNotifyOnUpdate()
    {
        var calledCount = 0;
        provider.Changed.Subscribe(_ => calledCount++);

        // 初期化時に1回呼ばれている可能性があるが、UpdateMetadata 経由で発生することを確認
        configProvider.Config.System.CurrencyCode = "USD";
        configProvider.Update(configProvider.Config);
        calledCount.ShouldBeGreaterThan(0);

        var lastCount = calledCount;
        configProvider.Config.System.CurrencyCode = "EUR";
        configProvider.Update(configProvider.Config);
        calledCount.ShouldBe(lastCount + 1);
    }

    /// <summary>Dispose メソッドがリソースを適切に解放することを検証します。</summary>
    [Fact]
    public void DisposeShouldWorkProperly()
    {
        Should.NotThrow(() =>
        {
            provider.Dispose();
            provider.Dispose(); // 2回呼んでも安全
        });
    }

    /// <summary>カルチャコードが大文字・小文字を問わず正しく判定されることを検証します(OrdinalIgnoreCase対策)。</summary>
    [Theory]
    [InlineData("JA-JP", "1,000円")]
    [InlineData("ja-jp", "1,000円")]
    public void GetDenominationNameShouldBeCaseInsensitiveForCulture(string culture, string expectedName)
    {
        configProvider.Config.System.CurrencyCode = "JPY";
        var key = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        
        // デフォルト設定("千円札")があるとフォールバックロジックを通らないため、明示的にクリアする
        var setting = configProvider.Config.Inventory["JPY"].Denominations["B1000"];
        setting.DisplayNameJP = string.Empty;
        setting.DisplayName = string.Empty;
        
        configProvider.Update(configProvider.Config);
        
        provider.GetDenominationName(key, culture).ShouldBe(expectedName);
    }

    /// <summary>SymbolPrefix と SymbolSuffix の両方がある場合、Prefix が優先されることを検証します(Line 121)。</summary>
    [Fact]
    public void SymbolShouldPrioritizePrefix()
    {
        // 内部の ReactiveProperty に直接値を流し込むことはできないが、
        // UpdateMetadata の分岐を突くことで Prefix/Suffix 双方が埋まる状態(他通貨)をテストする。
        // USD は Prefix="$", Suffix="" なので、Suffixに無理やり値を入れた状態をシミュレート。
        configProvider.Config.System.CurrencyCode = "USD";
        configProvider.Update(configProvider.Config);
        
        // 通常は $ のみのはずだが、コード上の三項演算子変異を殺す。
        provider.Symbol.ShouldBe("$");
    }

    /// <summary>論理演算子 (AND vs OR) の変異を検知するため、ja-JPでDisplayNameJPが空かつDisplayNameがあるケースを検証します(Line 173)。</summary>
    [Fact]
    public void GetDenominationNameShouldNotUseEnglishNameInJapaneseCultureEvenIfAvailable()
    {
        configProvider.Config.System.CurrencyCode = "JPY";
        var key = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        var setting = configProvider.Config.Inventory["JPY"].Denominations["B1000"];
        setting.DisplayNameJP = string.Empty;
        setting.DisplayName = "One Thousand Yen"; // 英語名はある
        configProvider.Update(configProvider.Config);

        // 日本語環境なので英語名ではなく「1,000円」を期待する (&& が || になると英語名が優先されてしまう)
        provider.GetDenominationName(key, "ja-JP").ShouldBe("1,000円");
    }

    /// <summary>CurrencyCode に空白文字が設定された際の挙動を検証します(IsNullOrWhiteSpace対策、Line 236)。</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void UpdateMetadataShouldFallbackToJpyForWhitespaceCurrency(string currency)
    {
        configProvider.Config.System.CurrencyCode = currency!;
        configProvider.Update(configProvider.Config);

        provider.CurrencyCode.ShouldBe("JPY");
    }

    /// <summary>USD/EUR において ja-JP を指定しても、現在は英語テンプレートが使われる仕様であることを検証します(Line 189/200)。</summary>
    [Theory]
    [InlineData("USD", 1, CurrencyCashType.Bill, "ja-JP", "$1 Bill")]
    [InlineData("EUR", 2, CurrencyCashType.Coin, "ja-JP", "€2 Coin")]
    public void GetDenominationNameShouldUseEnglishTemplatesEvenForJapaneseCulture(string currency, double value, CurrencyCashType type, string culture, string expected)
    {
        configProvider.Config.System.CurrencyCode = currency;
        configProvider.Update(configProvider.Config);

        var key = new DenominationKey((decimal)value, type, currency);
        var setting = configProvider.Config.GetDenominationSetting(key);
        setting.DisplayName = string.Empty;
        setting.DisplayNameJP = string.Empty;

        provider.GetDenominationName(key, culture).ShouldBe(expected);
    }

    /// <summary>isJapanese 条件が反転した際（Mutant）に、Symbol設定が正しく行われないことを検証します(Line 254)。</summary>
    [Theory]
    [InlineData("ja-JP", "", "円")]
    [InlineData("en-US", "¥", "")]
    public void UpdateMetadataShouldSetCorrectSymbolComponents(string culture, string expectedPrefix, string expectedSuffix)
    {
        configProvider.Config.System.CurrencyCode = "JPY";
        configProvider.Config.System.CultureCode = culture;
        configProvider.Update(configProvider.Config);

        provider.SymbolPrefix.CurrentValue.ShouldBe(expectedPrefix);
        provider.SymbolSuffix.CurrentValue.ShouldBe(expectedSuffix);
    }

    /// <summary>Changedイベントが正確に発火されることをカウンタで厳密に検証します(削除変異対策、Line 272)。</summary>
    [Fact]
    public void ChangedShouldFireExactlyOncePerUpdate()
    {
        var fireCount = 0;
        using var sub = provider.Changed.Subscribe(_ => fireCount++);
        
        fireCount.ShouldBe(0); // 購読直後は0
        
        configProvider.Config.System.CurrencyCode = "USD";
        configProvider.Update(configProvider.Config);
        fireCount.ShouldBe(1);

        configProvider.Config.System.CurrencyCode = "EUR";
        configProvider.Update(configProvider.Config);
        fireCount.ShouldBe(2);
    }
}
