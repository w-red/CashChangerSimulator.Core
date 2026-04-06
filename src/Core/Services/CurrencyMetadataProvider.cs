using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using R3;

namespace CashChangerSimulator.Core.Services;

/// <summary>通貨コードに基づいて通貨のメタデータを提供するサービス。</summary>
public class CurrencyMetadataProvider : ICurrencyMetadataProvider, IDisposable
{
    // ハードコードされた主要通貨の金種定義
    private static readonly Dictionary<string, (string DefaultSymbol, DenominationKey[] Denominations)> CurrencyDatabase = new(StringComparer.OrdinalIgnoreCase)
    {
        {
            "JPY",
            ("¥", new[]
            {
                new DenominationKey(10000, CurrencyCashType.Bill, "JPY"),
                new DenominationKey(5000, CurrencyCashType.Bill, "JPY"),
                new DenominationKey(2000, CurrencyCashType.Bill, "JPY"),
                new DenominationKey(1000, CurrencyCashType.Bill, "JPY"),
                new DenominationKey(500, CurrencyCashType.Coin, "JPY"),
                new DenominationKey(100, CurrencyCashType.Coin, "JPY"),
                new DenominationKey(50, CurrencyCashType.Coin, "JPY"),
                new DenominationKey(10, CurrencyCashType.Coin, "JPY"),
                new DenominationKey(5, CurrencyCashType.Coin, "JPY"),
                new DenominationKey(1, CurrencyCashType.Coin, "JPY")
            })
        },
        {
            "USD",
            ("$", new[]
            {
                new DenominationKey(100, CurrencyCashType.Bill, "USD"),
                new DenominationKey(50, CurrencyCashType.Bill, "USD"),
                new DenominationKey(20, CurrencyCashType.Bill, "USD"),
                new DenominationKey(10, CurrencyCashType.Bill, "USD"),
                new DenominationKey(5, CurrencyCashType.Bill, "USD"),
                new DenominationKey(1, CurrencyCashType.Bill, "USD"),
                new DenominationKey(1, CurrencyCashType.Coin, "USD"),
                new DenominationKey(0.5m, CurrencyCashType.Coin, "USD"),
                new DenominationKey(0.25m, CurrencyCashType.Coin, "USD"),
                new DenominationKey(0.1m, CurrencyCashType.Coin, "USD"),
                new DenominationKey(0.05m, CurrencyCashType.Coin, "USD"),
                new DenominationKey(0.01m, CurrencyCashType.Coin, "USD")
            })
        },
        {
            "EUR",
            ("€", new[]
            {
                new DenominationKey(500, CurrencyCashType.Bill, "EUR"),
                new DenominationKey(200, CurrencyCashType.Bill, "EUR"),
                new DenominationKey(100, CurrencyCashType.Bill, "EUR"),
                new DenominationKey(50, CurrencyCashType.Bill, "EUR"),
                new DenominationKey(20, CurrencyCashType.Bill, "EUR"),
                new DenominationKey(10, CurrencyCashType.Bill, "EUR"),
                new DenominationKey(5, CurrencyCashType.Bill, "EUR"),
                new DenominationKey(2, CurrencyCashType.Coin, "EUR"),
                new DenominationKey(1, CurrencyCashType.Coin, "EUR"),
                new DenominationKey(0.5m, CurrencyCashType.Coin, "EUR"),
                new DenominationKey(0.2m, CurrencyCashType.Coin, "EUR"),
                new DenominationKey(0.1m, CurrencyCashType.Coin, "EUR"),
                new DenominationKey(0.05m, CurrencyCashType.Coin, "EUR"),
                new DenominationKey(0.02m, CurrencyCashType.Coin, "EUR"),
                new DenominationKey(0.01m, CurrencyCashType.Coin, "EUR")
            })
        }
    };

    private readonly BindableReactiveProperty<string> symbolPrefix;
    private readonly BindableReactiveProperty<string> symbolSuffix;
    private readonly BindableReactiveProperty<string> currencyCodeProperty;
    private readonly BindableReactiveProperty<string> cultureCodeProperty;
    private readonly ConfigurationProvider configProvider;
    private readonly Subject<Unit> changed = new();
    private readonly CompositeDisposable disposables = [];

    /// <summary>Initializes a new instance of the <see cref="CurrencyMetadataProvider"/> class.設定プロバイダーを指定してメタデータプロバイダーを初期化する。</summary>
    /// <param name="configProvider">設定プロバイダー。</param>
    public CurrencyMetadataProvider(ConfigurationProvider configProvider)
    {
        ArgumentNullException.ThrowIfNull(configProvider);
        this.configProvider = configProvider;
        var initialConfig = configProvider.Config;
        currencyCodeProperty = new BindableReactiveProperty<string>(string.IsNullOrWhiteSpace(initialConfig.System.CurrencyCode) ? "JPY" : initialConfig.System.CurrencyCode);
        cultureCodeProperty = new BindableReactiveProperty<string>(initialConfig.System.CultureCode ?? "en-US");
        symbolPrefix = new BindableReactiveProperty<string>(string.Empty);
        symbolSuffix = new BindableReactiveProperty<string>(string.Empty);

        SymbolPrefix = symbolPrefix.ToReadOnlyReactiveProperty();
        SymbolSuffix = symbolSuffix.ToReadOnlyReactiveProperty();

        UpdateMetadata(initialConfig);

        configProvider.Reloaded.Subscribe(_ =>
        {
            UpdateMetadata(configProvider.Config);
        }).AddTo(disposables);
    }

    /// <inheritdoc/>
    public Observable<Unit> Changed => changed;

    /// <summary>通貨コード（例: "JPY"）。</summary>
    public string CurrencyCode => currencyCodeProperty.Value;

    /// <summary>通貨記号（プレフィックス優先）。</summary>
    public string Symbol => !string.IsNullOrEmpty(symbolPrefix.Value) ? symbolPrefix.Value : symbolSuffix.Value;

    /// <summary>通貨記号のプレフィックス（例: "¥", "$"）。通常、金額の前に表示されます。</summary>
    public ReadOnlyReactiveProperty<string> SymbolPrefix { get; }

    /// <summary>通貨記号のサフィックス（例: "円"）。通常、金額の後ろに表示されます。</summary>
    public ReadOnlyReactiveProperty<string> SymbolSuffix { get; }

    /// <summary>この通貨でサポートされている全金種のリスト（額面の降順）。</summary>
    public IReadOnlyList<DenominationKey> SupportedDenominations { get; private set; } = [];

    /// <summary>指定された金種の表示名を取得する。現在のカルチャ設定に従います。</summary>
    /// <param name="key">金種キー。</param>
    /// <returns>表示名。</returns>
    public string GetDenominationName(DenominationKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return GetDenominationName(key, cultureCodeProperty.Value);
    }

    /// <summary>指定された金種とカルチャの表示名を取得する。</summary>
    /// <param name="key">金種キー。</param>
    /// <param name="cultureCode">カルチャコード。</param>
    /// <returns>表示名。</returns>
    public string GetDenominationName(DenominationKey key, string cultureCode)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(cultureCode);
        var isJapanese = cultureCode.StartsWith("ja", StringComparison.OrdinalIgnoreCase);

        if (CurrencyCode.Equals("JPY", StringComparison.OrdinalIgnoreCase))
        {
            var setting = configProvider.Config.GetDenominationSetting(key);
            if (isJapanese && !string.IsNullOrEmpty(setting.DisplayNameJP))
            {
                return setting.DisplayNameJP;
            }

            if (!isJapanese && !string.IsNullOrEmpty(setting.DisplayName))
            {
                return setting.DisplayName;
            }

            return isJapanese ? $"{key.Value:N0}円" : $"{key.Value:N0} Yen";
        }

        if (CurrencyCode.Equals("USD", StringComparison.OrdinalIgnoreCase))
        {
            var setting = configProvider.Config.GetDenominationSetting(key);
            if (!string.IsNullOrEmpty(setting.DisplayName))
            {
                return setting.DisplayName;
            }

            return key.Type == CurrencyCashType.Bill ? $"${key.Value:N0} Bill" : $"${key.Value} Coin";
        }

        if (CurrencyCode.Equals("EUR", StringComparison.OrdinalIgnoreCase))
        {
            var setting = configProvider.Config.GetDenominationSetting(key);
            if (!string.IsNullOrEmpty(setting.DisplayName))
            {
                return setting.DisplayName;
            }

            return key.Type == CurrencyCashType.Bill ? $"€{key.Value:N0} Note" : $"€{key.Value} Coin";
        }

        var fallbackSetting = configProvider.Config.GetDenominationSetting(key);
        if (isJapanese && !string.IsNullOrEmpty(fallbackSetting.DisplayNameJP))
        {
            return fallbackSetting.DisplayNameJP;
        }

        if (!isJapanese && !string.IsNullOrEmpty(fallbackSetting.DisplayName))
        {
            return fallbackSetting.DisplayName;
        }

        return $"{key.Value:N0} ({key.Type})";
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>リソースを解放します。</summary>
    /// <param name="disposing">明示的な破棄かどうか。</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            disposables.Dispose();
            changed.Dispose();
            symbolPrefix.Dispose();
            symbolSuffix.Dispose();
            currencyCodeProperty.Dispose();
            cultureCodeProperty.Dispose();
        }
    }

    private void UpdateMetadata(SimulatorConfiguration config)
    {
        currencyCodeProperty.Value = string.IsNullOrWhiteSpace(config.System.CurrencyCode) ? "JPY" : config.System.CurrencyCode;
        cultureCodeProperty.Value = config.System.CultureCode ?? "en-US";

        var currentCurrency = currencyCodeProperty.Value;
        var currentCulture = cultureCodeProperty.Value;

        if (!CurrencyDatabase.TryGetValue(currentCurrency, out var currencyData))
        {
            currencyData = CurrencyDatabase["JPY"];
        }

        // 通貨記号を取得
        var rawSymbol = currencyData.DefaultSymbol;

        // JPY の場合、ロケールによって表示位置を調整する
        if (currentCurrency.Equals("JPY", StringComparison.OrdinalIgnoreCase))
        {
            var isJapanese = currentCulture.StartsWith("ja", StringComparison.OrdinalIgnoreCase);
            if (isJapanese)
            {
                symbolPrefix.Value = string.Empty;
                symbolSuffix.Value = "円";
            }
            else
            {
                symbolPrefix.Value = "¥";
                symbolSuffix.Value = string.Empty;
            }
        }
        else
        {
            symbolPrefix.Value = rawSymbol;
            symbolSuffix.Value = string.Empty;
        }

        SupportedDenominations = currencyData.Denominations;
        changed.OnNext(Unit.Default);
    }
}
