using CashChangerSimulator.Core.Models;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device;

/// <summary>UPOS の通貨に関わる計算や単位の生成を行うヘルパークラス。</summary>
public static class UposCurrencyHelper
{
    /// <summary>指定された通貨コードに対する係数 (セント等への変換率) を取得します。</summary>
    public static decimal GetCurrencyFactor(string currencyCode)
    {
        return currencyCode switch
        {
            "USD" or "EUR" or "GBP" or "CAD" or "AUD" => 100m,
            _ => 1m
        };
    }

    /// <summary>DenominationKey から NominalValue (整数化された額面) を取得します。</summary>
    public static int GetNominalValue(DenominationKey key)
    {
        return (int)Math.Round(key.Value * GetCurrencyFactor(key.CurrencyCode));
    }

    /// <summary>在庫情報からアクティブな通貨コードに対する CashUnits (硬貨と紙幣の額面一覧) を生成します。</summary>
    public static CashUnits BuildCashUnits(Inventory inventory, string activeCurrencyCode)
    {
        var activeUnits = inventory.AllCounts
            .Where(kv => kv.Key.CurrencyCode == activeCurrencyCode)
            .OrderBy(kv => kv.Key.Value)
            .ToList();

        var coins = activeUnits
            .Where(kv => kv.Key.Type == CurrencyCashType.Coin)
            .Select(kv => GetNominalValue(kv.Key))
            .ToArray();

        var bills = activeUnits
            .Where(kv => kv.Key.Type == CurrencyCashType.Bill)
            .Select(kv => GetNominalValue(kv.Key))
            .ToArray();

        return new CashUnits(coins, bills);
    }
}
