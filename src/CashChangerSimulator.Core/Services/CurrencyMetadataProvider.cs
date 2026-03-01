using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using MoneyKind4Opos.Codes;
using MoneyKind4Opos.Currencies.Interfaces;
using R3;
using System.Reflection;

namespace CashChangerSimulator.Core.Services;

/// <summary>通貨コードに基づいて、MoneyKind4Opos から通貨のメタデータを提供するサービス。</summary>
public class CurrencyMetadataProvider
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

        // MoneyKind4Opos アセンブリから対応する通貨クラスを探す
        var currencyType = FindCurrencyType(currencyCode) ?? FindCurrencyType("JPY")!;

        // 通貨記号を取得
        var rawSymbol = GetStaticPropertyValue<CurrencyFormattingOptions>(currencyType, "Local")?.Symbol ?? "";
        
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


        // 金種リストを構築
        var denominations = new List<DenominationKey>();

        var coins = GetStaticPropertyValue<IEnumerable<CashFaceInfo>>(currencyType, "Coins");
        if (coins != null)
        {
            denominations.AddRange(coins.Select(c => new DenominationKey(c.Value, CashType.Coin)));
        }

        var bills = GetStaticPropertyValue<IEnumerable<CashFaceInfo>>(currencyType, "Bills");
        if (bills != null)
        {
            denominations.AddRange(bills.Select(b => new DenominationKey(b.Value, CashType.Bill)));
        }

        // 額面の降順、同じ額面なら紙幣優先でソート
        SupportedDenominations = [.. denominations
            .OrderByDescending(d => d.Value)
            .ThenByDescending(d => d.Type)];
    }

    /// <summary>指定された金種の表示名を取得する。現在のカルチャ設定に従います。</summary>
    public string GetDenominationName(DenominationKey key) => GetDenominationName(key, _cultureCode.Value);

    /// <summary>指定された金種とカルチャの表示名を取得する。</summary>
    public string GetDenominationName(DenominationKey key, string cultureCode)
    {
        var isJapanese = cultureCode.StartsWith("ja", StringComparison.OrdinalIgnoreCase);

        // JPY の場合の特化処理
        if (CurrencyCode.Equals("JPY", StringComparison.OrdinalIgnoreCase))
        {
            if (isJapanese)
            {
                return $"{key.Value}円";
            }
            else
            {
                // Format with group separator for English (e.g., 10,000 Yen)
                return $"{key.Value:N0} Yen";
            }
        }

        var currencyType = FindCurrencyType(CurrencyCode) ?? FindCurrencyType("JPY")!;
        var faces = GetStaticPropertyValue<IEnumerable<CashFaceInfo>>(currencyType, key.Type == CashType.Coin ? "Coins" : "Bills");
        var face = faces?.FirstOrDefault(f => f.Value == key.Value);

        if (face != null)
        {
            // MoneyKind4Opos の Name を使用（例: "100 Yen Coin", "1 Dollar Bill"）
            return face.Name ?? $"{key.Value} ({key.Type})";
        }

        return $"{key.Value} ({key.Type})";
    }

    private static Type? FindCurrencyType(string code)
    {
        return typeof(ICurrency).Assembly.GetTypes()
            .FirstOrDefault(t =>
                typeof(ICurrency).IsAssignableFrom(t) &&
                !t.IsInterface && !t.IsAbstract &&
                GetStaticPropertyValue<Iso4217>(t, "Code").ToString().Equals(code, StringComparison.OrdinalIgnoreCase));
    }

    private static T? GetStaticPropertyValue<T>(Type type, string propertyName)
    {
        var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
        return (T?)prop?.GetValue(null);
    }
}
