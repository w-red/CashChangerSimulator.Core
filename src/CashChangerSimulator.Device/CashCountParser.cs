using CashChangerSimulator.Core.Models;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device;

/// <summary>UPOSの文字列形式を解析し、CashCount のリストに変換するパーサクラス。</summary>
/// <remarks>
/// 例: "100,500:5;1000:10" のような形式を解析します。
/// セミコロン区切りで硬貨(Coins)と紙幣(Bills)を識別します。
/// </remarks>
public static class CashCountParser
{
    /// <summary>文字列をパースし、現在の Inventory 定義と照らし合わせて CashCount リストを生成します。</summary>
    /// <param name="input">UPOS形式の文字列 (例: "50,100:10;1000:5")</param>
    /// <param name="activeKeys">現在インベントリに登録されている有効な金種キーのリスト</param>
    /// <param name="currencyFactor">通貨の係数（USDなら100, JPYなら1）</param>
    /// <returns>パースされた CashCount のリスト</returns>
    /// <exception cref="ArgumentException">フォーマットが不正な場合、または曖昧な値の場合にスローされます。</exception>
    public static IEnumerable<CashCount> Parse(string input, IEnumerable<DenominationKey> activeKeys, decimal currencyFactor)
    {
        ArgumentNullException.ThrowIfNull(activeKeys);
        if (string.IsNullOrWhiteSpace(input)) return [];

        var sections = input.Split(';');
        if (sections.Length > 2)
            throw new ArgumentException("Invalid cash count format: Too many semicolon sections. Expected at most 'Coins;Bills'.");

        return sections.SelectMany((section, index) =>
        {
            var trimmedSection = section.Trim();
            if (string.IsNullOrEmpty(trimmedSection)) return [];

            // UPOS Standard: 0 = Coin, 1 = Bill (if 2 sections exist)
            var sectionType = sections.Length == 2
                ? (index == 0 ? CurrencyCashType.Coin : CurrencyCashType.Bill)
                : (CurrencyCashType?)null;

            return trimmedSection.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => ParsePart(part, sectionType, activeKeys, currencyFactor));
        }).ToList();
    }

    private static CashCount ParsePart(string part, CurrencyCashType? sectionType, IEnumerable<DenominationKey> activeKeys, decimal currencyFactor)
    {
        var pair = part.Split(':');
        if (pair.Length != 2)
            throw new ArgumentException($"Invalid cash count format: '{part}'. Expected format 'Value:Count'.");

        var keyStr = NormalizeNominalValue(pair[0].Trim());
        if (!decimal.TryParse(keyStr, out var decimalValue))
            throw new ArgumentException($"Invalid nominal value: '{keyStr}' in '{part}'.");

        if (!int.TryParse(pair[1].Trim(), out var count) || count < 0)
            throw new ArgumentException($"Invalid count value: '{pair[1].Trim()}' in '{part}'.");

        var finalKey = FindKey(decimalValue, sectionType, activeKeys, part);

        return new CashCount(
            finalKey.Type == CurrencyCashType.Bill ? CashCountType.Bill : CashCountType.Coin,
            (int)(decimalValue * currencyFactor),
            count);
    }

    private static string NormalizeNominalValue(string value) =>
        value.StartsWith('.') ? "0" + value : value;

    private static DenominationKey FindKey(decimal decimalValue, CurrencyCashType? sectionType, IEnumerable<DenominationKey> activeKeys, string part)
    {
        var matchingKeys = activeKeys.Where(k => k.Value == decimalValue).ToList();

        if (matchingKeys.Count == 0)
            throw new ArgumentException($"Unsupported denomination value '{decimalValue}' found in '{part}'.");

        if (sectionType.HasValue)
        {
            return matchingKeys.FirstOrDefault(k => k.Type == sectionType.Value)
                ?? throw new ArgumentException($"Denomination '{decimalValue}' is not registered as a {sectionType} in the current inventory, but was found in the {sectionType} section.");
        }

        return matchingKeys.Count switch
        {
            1 => matchingKeys[0],
            > 1 => throw new ArgumentException($"Ambiguous denomination value '{decimalValue}' in '{part}'. Please use the 'Coins;Bills' semicolon format to resolve ambiguity."),
            _ => throw new ArgumentException($"Unsupported denomination value '{decimalValue}' found in '{part}'.")
        };
    }
}
