
namespace CashChangerSimulator.Core.Models;

/// <summary>金種を一意に識別するための複合キー（通貨コード、額面、硬貨/紙幣の種別）。</summary>
/// <param name="Value">額面。</param>
/// <param name="Type">硬貨/紙幣の種別。</param>
/// <param name="CurrencyCode">通貨コード。</param>
public record DenominationKey(
    decimal Value,
    CurrencyCashType Type,
    string CurrencyCode = DenominationKey.DefaultCurrencyCode)
{
    /// <summary>デフォルトの通貨コード。</summary>
    public const string DefaultCurrencyCode = "JPY";

    /// <summary>紙幣を表すプレフィックス文字。</summary>
    public const char BillPrefix = 'B';

    /// <summary>硬貨を表すプレフィックス文字。</summary>
    public const char CoinPrefix = 'C';

    /// <summary>キーのセパレータ文字。</summary>
    public const char KeySeparator = ':';

    /// <summary>種別に応じたプレフィックス文字を取得します。</summary>
    public char PrefixChar => Type == CurrencyCashType.Bill ? BillPrefix : CoinPrefix;

    /// <summary>設定ファイル等で使用する文字列形式を取得します（例: "B1000", "C500", "C0.25"）。</summary>
    public string ToDenominationString() => $"{PrefixChar}{Value}";

    /// <summary>文字列形式から金種キーを解析します。</summary>
    public static bool TryParse(string s, out DenominationKey? result)
    {
        return TryParse(s, DefaultCurrencyCode, out result);
    }

    /// <summary>通貨コードと文字列形式から金種キーを解析します。</summary>
    public static bool TryParse(string s, string currencyCode, out DenominationKey? result)
    {
        result = null;
        if (string.IsNullOrEmpty(s) || s.Length < 2) return false;

        var type = char.ToUpperInvariant(s[0]) switch
        {
            BillPrefix => CurrencyCashType.Bill,
            CoinPrefix => CurrencyCashType.Coin,
            _ => CurrencyCashType.Undefined
        };

        if (type == CurrencyCashType.Undefined) return false;

        if (decimal.TryParse(s[1..], out var value))
        {
            result = new DenominationKey(value, type, currencyCode);
            return true;
        }

        return false;
    }
}
