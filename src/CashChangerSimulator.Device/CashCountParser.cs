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
        if (string.IsNullOrWhiteSpace(input))
        {
            return [];
        }

        var results = new List<CashCount>();

        // UPOS Standard: Coins ; Bills
        var sections = input.Split(';');
        if (sections.Length > 2)
        {
            throw new ArgumentException("Invalid cash count format: Too many semicolon sections. Expected at most 'Coins;Bills'.");
        }

        for (int i = 0; i < sections.Length; i++)
        {
            var section = sections[i].Trim();
            if (string.IsNullOrEmpty(section)) continue;

            // Session context: 0 = Coin, 1 = Bill (if 2 sections exist)
            // If only 1 section exists and no semicolon, we treat it as "implicit" but following the same parser.
            CurrencyCashType? sectionType = null;
            if (sections.Length == 2)
            {
                sectionType = (i == 0) ? CurrencyCashType.Coin : CurrencyCashType.Bill;
            }

            var parts = section.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var pair = part.Split(':');
                if (pair.Length != 2)
                {
                    throw new ArgumentException($"Invalid cash count format: '{part}'. Expected format 'Value:Count'.");
                }

                var keyStr = pair[0].Trim();
                var countStr = pair[1].Trim();

                // Support .5 shorthand by prepending 0
                if (keyStr.StartsWith('.'))
                {
                    keyStr = "0" + keyStr;
                }

                if (!decimal.TryParse(keyStr, out decimal decimalValue))
                {
                    throw new ArgumentException($"Invalid nominal value: '{keyStr}' in '{part}'.");
                }

                if (!int.TryParse(countStr, out int count) || count < 0)
                {
                    throw new ArgumentException($"Invalid count value: '{countStr}' in '{part}'.");
                }

                // Find matching keys
                var matchingKeys = activeKeys.Where(k => k.Value == decimalValue).ToList();

                if (matchingKeys.Count == 0)
                {
                    throw new ArgumentException($"Unsupported denomination value '{decimalValue}' found in '{part}'.");
                }

                DenominationKey? finalKey = null;

                if (sectionType.HasValue)
                {
                    finalKey = matchingKeys.FirstOrDefault(k => k.Type == sectionType.Value);
                    if (finalKey == null)
                    {
                        // In strict section mode, if the value doesn't exist in this section, it's an error
                        throw new ArgumentException($"Denomination '{decimalValue}' is not registered as a {sectionType} in the current inventory, but was found in the {sectionType} section.");
                    }
                }
                else
                {
                    // Implicit matching (no semicolon context)
                    if (matchingKeys.Count > 1)
                    {
                        throw new ArgumentException($"Ambiguous denomination value '{decimalValue}' in '{part}'. Please use the 'Coins;Bills' semicolon format to resolve ambiguity.");
                    }
                    finalKey = matchingKeys.First();
                }

                results.Add(new CashCount(
                    finalKey.Type == CurrencyCashType.Bill ? CashCountType.Bill : CashCountType.Coin,
                    (int)(decimalValue * currencyFactor),
                    count));
            }
        }

        return results;
    }
}
