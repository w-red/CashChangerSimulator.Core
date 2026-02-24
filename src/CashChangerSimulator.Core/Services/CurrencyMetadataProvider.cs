using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using MoneyKind4Opos.Codes;
using MoneyKind4Opos.Currencies.Interfaces;
using System.Reflection;

namespace CashChangerSimulator.Core.Services;

/// <summary>
/// 通貨コードに基づいて、MoneyKind4Opos から通貨のメタデータを提供するサービス。
/// </summary>
public class CurrencyMetadataProvider
{
    private readonly Type _currencyType;

    /// <summary>通貨コード（例: "JPY"）。</summary>
    public string CurrencyCode { get; }

    /// <summary>通貨記号（例: "¥", "$"）。</summary>
    public string Symbol { get; }

    /// <summary>この通貨でサポートされている全金種のリスト（額面の降順）。</summary>
    public IReadOnlyList<DenominationKey> SupportedDenominations { get; }

    /// <summary>設定プロバイダーを指定してメタデータプロバイダーを初期化する。</summary>
    public CurrencyMetadataProvider(ConfigurationProvider configProvider)
    {
        CurrencyCode = string.IsNullOrWhiteSpace(configProvider.Config.CurrencyCode) ? "JPY" : configProvider.Config.CurrencyCode;
        var currencyCode = CurrencyCode;

        // MoneyKind4Opos アセンブリから対応する通貨クラスを探す
        _currencyType = FindCurrencyType(currencyCode) ?? FindCurrencyType("JPY")!;

        // 通貨記号を取得 (Local.Symbol)
        Symbol = GetStaticPropertyValue<CurrencyFormattingOptions>(_currencyType, "Local")?.Symbol ?? "";

        // 金種リストを構築
        var denominations = new List<DenominationKey>();

        var coins = GetStaticPropertyValue<IEnumerable<CashFaceInfo>>(_currencyType, "Coins");
        if (coins != null)
        {
            denominations.AddRange(coins.Select(c => new DenominationKey(c.Value, CashType.Coin)));
        }

        var bills = GetStaticPropertyValue<IEnumerable<CashFaceInfo>>(_currencyType, "Bills");
        if (bills != null)
        {
            denominations.AddRange(bills.Select(b => new DenominationKey(b.Value, CashType.Bill)));
        }

        // 額面の降順、同じ額面なら紙幣優先でソート
        SupportedDenominations = [.. denominations
            .OrderByDescending(d => d.Value)
            .ThenByDescending(d => d.Type)];
    }

    /// <summary>指定された金種の表示名を取得する。</summary>
    public string GetDenominationName(DenominationKey key)
    {
        var faces = GetStaticPropertyValue<IEnumerable<CashFaceInfo>>(_currencyType, key.Type == CashType.Coin ? "Coins" : "Bills");
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
