using CashChangerSimulator.Core.Models;
using Microsoft.PointOfService;
using MoneyKind4Opos.Currencies.Interfaces;

namespace CashChangerSimulator.Device;

/// <summary>
/// UPOSの文字列形式（例: "1000:10,500:5" や "B1:10,C1:5"）を解析し、
/// IEnumerable&lt;CashCount&gt; に変換する機能を提供するパーサクラス。
/// </summary>
public static class CashCountParser
{
    /// <summary>
    /// 文字列をパースし、現在の Inventory 定義と照らし合わせて CashCount リストを生成します。
    /// </summary>
    /// <param name="input">UPOS形式の文字列 (例: "1000:10,500:5")</param>
    /// <param name="activeKeys">現在インベントリに登録されている有効な金種キーのリスト</param>
    /// <returns>パースされた CashCount のリスト</returns>
    /// <exception cref="ArgumentException">フォーマットが不正な場合、または曖昧な値（両方のTypeが存在し、プレフィックスがない等）の場合にスローされます。</exception>
    public static IEnumerable<CashCount> Parse(string input, IEnumerable<DenominationKey> activeKeys)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return [];
        }

        var results = new List<CashCount>();
        var parts = input.Split(',', StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var pair = part.Split(':');
            if (pair.Length != 2)
            {
                throw new ArgumentException($"Invalid cash count format: '{part}'. Expected format 'Value:Count'.");
            }

            var keyStr = pair[0].Trim();
            var countStr = pair[1].Trim();

            if (!int.TryParse(countStr, out int count) || count < 0)
            {
                throw new ArgumentException($"Invalid count value: '{countStr}' in '{part}'.");
            }

            bool hasExplicitPrefix = false;
            CashType? explicitType = null;

            if (keyStr.StartsWith("B", StringComparison.OrdinalIgnoreCase))
            {
                hasExplicitPrefix = true;
                explicitType = CashType.Bill;
                keyStr = keyStr.Substring(1);
            }
            else if (keyStr.StartsWith("C", StringComparison.OrdinalIgnoreCase))
            {
                hasExplicitPrefix = true;
                explicitType = CashType.Coin;
                keyStr = keyStr.Substring(1);
            }

            if (!decimal.TryParse(keyStr, out decimal nominalValue))
            {
                throw new ArgumentException($"Invalid nominal value: '{keyStr}' in '{part}'.");
            }

            // Find matching keys in the inventory based on the nominal value
            var matchingKeys = activeKeys.Where(k => k.Value == nominalValue).ToList();

            if (matchingKeys.Count == 0)
            {
                throw new ArgumentException($"Unsupported denomination value '{nominalValue}' found in '{part}'. Make sure it exists in the active inventory configuration.");
            }

            if (hasExplicitPrefix)
            {
                var specificKey = matchingKeys.FirstOrDefault(k => k.Type == explicitType);
                if (specificKey == null)
                {
                    throw new ArgumentException($"Unsupported denomination explicitly requested: {explicitType} {nominalValue} in '{part}'.");
                }
                results.Add(new CashCount(
                    specificKey.Type == CashType.Bill ? CashCountType.Bill : CashCountType.Coin,
                    (int)nominalValue,
                    count));
            }
            else
            {
                // Implicit matching
                if (matchingKeys.Count > 1)
                {
                    throw new ArgumentException($"Ambiguous denomination value '{nominalValue}' in '{part}'. The inventory contains both a bill and a coin for this value. Please use explicit prefixes 'B' or 'C' (e.g., 'B{nominalValue}:Count').");
                }

                var key = matchingKeys.First();
                results.Add(new CashCount(
                    key.Type == CashType.Bill ? CashCountType.Bill : CashCountType.Coin,
                    (int)nominalValue,
                    count));
            }
        }

        return results;
    }
}
