using CashChangerSimulator.Core.Models;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device;

/// <summary>UPOS CashCount と内部 DenominationKey 間の変換を担うアダプター。</summary>
/// <remarks>
/// UPOS 標準のデータ構造（CashCount）と、シミュレータ内部で管理する金種キー（DenominationKey）の相互変換を提供します。
/// 通貨コードや通貨換算係数（factor）を考慮して、正確な金額計算を行います。
/// </remarks>
public static class CashCountAdapter
{
    /// <summary>CashCount を DenominationKey に変換します。</summary>
    public static DenominationKey ToDenominationKey(CashCount cc, string currencyCode, decimal factor)
    {
        var type = cc.Type == CashCountType.Bill ? CurrencyCashType.Bill : CurrencyCashType.Coin;
        var value = cc.NominalValue / factor;
        return new DenominationKey(value, type, currencyCode);
    }

    /// <summary>DenominationKey を CashCount に変換します。</summary>
    public static CashCount ToCashCount(DenominationKey key, int count, decimal factor)
    {
        var type = key.Type == CurrencyCashType.Bill ? CashCountType.Bill : CashCountType.Coin;
        var nominalValue = (int)Math.Round(key.Value * factor);
        return new CashCount(type, nominalValue, count);
    }

    /// <summary>CashCount 配列を DenominationKey→枚数の辞書に一括変換します。</summary>
    public static Dictionary<DenominationKey, int> ToDenominationDict(
        IEnumerable<CashCount> cashCounts, string currencyCode, decimal factor)
    {
        ArgumentNullException.ThrowIfNull(cashCounts);
        ArgumentNullException.ThrowIfNull(currencyCode);

        var dict = new Dictionary<DenominationKey, int>();
        foreach (var cc in cashCounts)
        {
            if (cc.Count < 0)
            {
                throw new PosControlException("Count cannot be negative.", ErrorCode.Illegal);
            }
            var key = ToDenominationKey(cc, currencyCode, factor);
            dict[key] = cc.Count;
        }
        return dict;
    }

    /// <summary>
    /// 文字列形式の金種・枚数リスト ("denom:count,denom:count") を CashCount 配列に変換します。
    /// </summary>
    public static IEnumerable<CashCount> ParseCashCounts(string countsStr, string currencyCode, decimal factor, IEnumerable<DenominationKey> availableKeys)
    {
        ArgumentNullException.ThrowIfNull(currencyCode);
        ArgumentNullException.ThrowIfNull(availableKeys);
        if (string.IsNullOrWhiteSpace(countsStr)) return [];

        return countsStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(pair => pair.Split(':', StringSplitOptions.TrimEntries))
            .Where(parts => parts.Length == 2 && decimal.TryParse(parts[0], out _) && int.TryParse(parts[1], out _))
            .Select(parts =>
            {
                var denomVal = decimal.Parse(parts[0]);
                var count = int.Parse(parts[1]);
                var val = denomVal / factor;

                // Find matching denomination to determine if it's a Bill or Coin
                var match = availableKeys.FirstOrDefault(k => k.Value == val && k.CurrencyCode == currencyCode);
                var type = match?.Type == CurrencyCashType.Bill ? CashCountType.Bill : CashCountType.Coin;
                var nominalValue = (int)Math.Round(val * factor);

                return new CashCount(type, nominalValue, count);
            }).ToList();
    }

    /// <summary>
    /// CashCount 配列を文字列形式 ("denom:count,denom:count") に変換します。
    /// </summary>
    public static string FormatCashCounts(IEnumerable<CashCount> cashCounts)
    {
        ArgumentNullException.ThrowIfNull(cashCounts);
        return string.Join(",", cashCounts.Select(cc => $"{cc.NominalValue}:{cc.Count}"));
    }
}
