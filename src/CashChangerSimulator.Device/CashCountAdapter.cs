using CashChangerSimulator.Core.Models;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device;

/// <summary>UPOS CashCount と内部 DenominationKey 間の変換を担うアダプター。</summary>
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
}
