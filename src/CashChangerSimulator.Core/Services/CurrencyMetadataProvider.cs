using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using R3;

namespace CashChangerSimulator.Core.Services;

/// <summary>通貨コードに基づいて通貨のメタデータを提供するサービス。</summary>
public class CurrencyMetadataProvider : ICurrencyMetadataProvider
{
    private readonly BindableReactiveProperty<string> _symbolPrefix;
    private readonly BindableReactiveProperty<string> _symbolSuffix;
    private readonly BindableReactiveProperty<string> _currencyCode;
    private readonly BindableReactiveProperty<string> _cultureCode;

    /// <summary>通貨コード（例: "JPY"）。</summary>
    public string CurrencyCode => _currencyCode.Value;

    /// <summary>通貨記号（プレフィックス優先）。</summary>
    public string Symbol => !string.IsNullOrEmpty(_symbolPrefix.Value) ? _symbolPrefix.Value : _symbolSuffix.Value;

    /// <summary>通貨記号のプレフィックス（例: "¥", "$"）。通常、金額の前に表示されます。</summary>
    public ReadOnlyReactiveProperty<string> SymbolPrefix { get; }

    /// <summary>通貨記号のサフィックス（例: "円"）。通常、金額の後ろに表示されます。</summary>
    public ReadOnlyReactiveProperty<string> SymbolSuffix { get; }

    /// <summary>この通貨でサポートされている全金種のリスト（額面の降順）。</summary>
    public IReadOnlyList<DenominationKey> SupportedDenominations { get; private set; } = [];

    // ハードコードされた主要通貨の金種定義
    private static readonly Dictionary<string, (string DefaultSymbol, DenominationKey[] Denominations)> _currencyDatabase = new(StringComparer.OrdinalIgnoreCase)
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

    /// <summary>設定プロバイダーを指定してメタデータプロバイダーを初期化する。</summary>
    public CurrencyMetadataProvider(ConfigurationProvider configProvider)
    {
        _currencyCode = new BindableReactiveProperty<string>(string.IsNullOrWhiteSpace(configProvider.Config.System.CurrencyCode) ? "JPY" : configProvider.Config.System.CurrencyCode);
        _cultureCode = new BindableReactiveProperty<string>(configProvider.Config.System.CultureCode ?? "en-US");
        _symbolPrefix = new BindableReactiveProperty<string>("");
        _symbolSuffix = new BindableReactiveProperty<string>("");

        SymbolPrefix = _symbolPrefix.ToReadOnlyReactiveProperty();
        SymbolSuffix = _symbolSuffix.ToReadOnlyReactiveProperty();

        UpdateMetadata(configProvider.Config);

        configProvider.Reloaded.Subscribe(_ =>
        {
            UpdateMetadata(configProvider.Config);
        });
    }

    private void UpdateMetadata(SimulatorConfiguration config)
    {
        _currencyCode.Value = string.IsNullOrWhiteSpace(config.System.CurrencyCode) ? "JPY" : config.System.CurrencyCode;
        _cultureCode.Value = config.System.CultureCode ?? "en-US";

        var currencyCode = _currencyCode.Value;
        var cultureCode = _cultureCode.Value;

        if (!_currencyDatabase.TryGetValue(currencyCode, out var currencyData))
        {
            currencyData = _currencyDatabase["JPY"];
        }

        // 通貨記号を取得
        var rawSymbol = currencyData.DefaultSymbol;

        // JPY の場合、ロケールによって表示位置を調整する
        if (currencyCode.Equals("JPY", StringComparison.OrdinalIgnoreCase))
        {
            var isJapanese = cultureCode.StartsWith("ja", StringComparison.OrdinalIgnoreCase);
            if (isJapanese)
            {
                _symbolPrefix.Value = "";
                _symbolSuffix.Value = "円";
            }
            else
            {
                _symbolPrefix.Value = "¥";
                _symbolSuffix.Value = "";
            }
        }
        else
        {
            _symbolPrefix.Value = rawSymbol;
            _symbolSuffix.Value = "";
        }

        SupportedDenominations = currencyData.Denominations;
    }

    /// <summary>指定された金種の表示名を取得する。現在のカルチャ設定に従います。</summary>
    public string GetDenominationName(DenominationKey key) => GetDenominationName(key, _cultureCode.Value);

    /// <summary>指定された金種とカルチャの表示名を取得する。</summary>
    public string GetDenominationName(DenominationKey key, string cultureCode)
    {
        var isJapanese = cultureCode.StartsWith("ja", StringComparison.OrdinalIgnoreCase);

        if (CurrencyCode.Equals("JPY", StringComparison.OrdinalIgnoreCase))
        {
            return isJapanese ? $"{key.Value:N0}円" : $"{key.Value:N0} Yen";
        }

        if (CurrencyCode.Equals("USD", StringComparison.OrdinalIgnoreCase))
        {
            return key.Type == CurrencyCashType.Bill ? $"${key.Value:N0} Bill" : $"${key.Value} Coin";
        }

        return CurrencyCode.Equals("EUR", StringComparison.OrdinalIgnoreCase)
            ? key.Type == CurrencyCashType.Bill ? $"€{key.Value:N0} Note" : $"€{key.Value} Coin"
            : $"{key.Value:N0} ({key.Type})";
    }
}
