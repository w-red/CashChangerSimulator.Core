using MemoryPack;

namespace CashChangerSimulator.Core.Models;

/// <summary>金種を一意に識別するための複合キー。</summary>
/// <param name="Value">金種の額面（例: 1000, 500, 0.25）。</param>
/// <param name="Type">金種の種別（紙幣または硬貨）。</param>
/// <param name="CurrencyCode">通貨コード（例: "JPY"）。</param>
/// <remarks>
/// 通貨コード（JPY等）、額面（1000等）、および硬貨/紙幣の種別を組み合わせたイミュータブルなキー。
/// 在庫管理や金額計算の最小単位として、ディクショナリのキー等に使用されます。
/// </remarks>
[MemoryPackable]
public partial record DenominationKey(
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

    /// <summary>Gets 種別に応じたプレフィックス文字を取得します。</summary>
    public char PrefixChar => Type == CurrencyCashType.Bill ? BillPrefix : CoinPrefix;

    /// <summary>額面を正規化した値を比較に使用します。</summary>
    /// <param name="other">比較対象のオブジェクト。</param>
    /// <returns>数値的に等しい場合は true、それ以外は false。</returns>
    public virtual bool Equals(DenominationKey? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Value == other.Value && Type == other.Type && CurrencyCode == other.CurrencyCode;
    }

    /// <summary>額面を正規化したハッシュ値を取得します。</summary>
    /// <returns>全属性に基づくハッシュ値。</returns>
    public override int GetHashCode()
    {
        // decimal.GetHashCode() は 1000m と 1000.00m で異なる値を返す可能性があるため、
        // 文字列変換（G29形式など）や正規化を使用してハッシュ値を安定させる必要があります。
        // ここでは最も安全な ToString("G29") によるハッシュ値生成を行います。
        return HashCode.Combine(
            Value.ToString("G29", System.Globalization.CultureInfo.InvariantCulture),
            Type,
            CurrencyCode);
    }

    /// <summary>設定ファイル等で使用する文字列形式を取得します（例: "B1000", "C500", "C0.25"）。</summary>
    /// <returns>文字列形式の金種。</returns>
    public string ToDenominationString() => $"{PrefixChar}{Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    /// <summary>文字列形式から金種キーを解析します。</summary>
    /// <param name="s">解析対象の文字列。</param>
    /// <param name="result">解析結果の金種キー。</param>
    /// <returns>成功した場合は true、それ以外は false。</returns>
    public static bool TryParse(string s, out DenominationKey? result)
    {
        return TryParse(s, DefaultCurrencyCode, out result);
    }

    /// <summary>通貨コードと文字列形式から金種キーを解析します。</summary>
    /// <param name="s">解析対象の文字列。</param>
    /// <param name="currencyCode">通貨コード（s に通貨コードが含まれない場合のデフォルトとして使用）。</param>
    /// <param name="result">解析結果の金種キー。</param>
    /// <returns>成功した場合は true、それ以外は false。</returns>
    public static bool TryParse(string s, string currencyCode, out DenominationKey? result)
    {
        ArgumentNullException.ThrowIfNull(currencyCode);
        result = null;

        if (string.IsNullOrEmpty(s))
        {
            return false;
        }

        // セパレータ（:）が含まれる場合は、通貨コードと金種文字列に分離する
        var parts = s.Split(KeySeparator, 2);
        string targetDenomination;
        string targetCurrency;

        if (parts.Length == 2)
        {
            targetCurrency = parts[0];
            targetDenomination = parts[1];
        }
        else
        {
            targetCurrency = currencyCode;
            targetDenomination = s;
        }

        if (string.IsNullOrEmpty(targetDenomination) || targetDenomination.Length < 2)
        {
            return false;
        }

        var type = char.ToUpperInvariant(targetDenomination[0]) switch
        {
            BillPrefix => CurrencyCashType.Bill,
            CoinPrefix => CurrencyCashType.Coin,
            _ => CurrencyCashType.Undefined
        };

        if (type == CurrencyCashType.Undefined)
        {
            return false;
        }

        if (decimal.TryParse(targetDenomination[1..], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var value))
        {
            result = new DenominationKey(value, type, targetCurrency);
            return true;
        }

        return false;
    }
}
