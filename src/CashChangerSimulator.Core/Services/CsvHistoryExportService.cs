using System.Text;
using CashChangerSimulator.Core.Transactions;

namespace CashChangerSimulator.Core.Services;

/// <summary>
/// 取引履歴を CSV 形式にエクスポートするサービスの実装。
/// </summary>
public class CsvHistoryExportService : IHistoryExportService
{
    private const string Header = "Timestamp,Type,Amount,Details";

    /// <inheritdoc/>
    public string Export(IEnumerable<TransactionEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var sb = new StringBuilder();
        sb.AppendLine(Header);

        foreach (var entry in entries)
        {
            var details = string.Join("|", entry.Counts.Select(c => $"{c.Key.CurrencyCode}-{c.Key.ToDenominationString()}:{c.Value}"));
            
            sb.Append(entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")).Append(",");
            sb.Append(entry.Type).Append(",");
            sb.Append(entry.Amount).Append(",");
            sb.Append(details);
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
