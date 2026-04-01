using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Core.Models;
using Shouldly;

namespace CashChangerSimulator.Tests.Core;

/// <summary>CsvHistoryExportService の CSV エクスポート機能を検証するテストクラス。</summary>
public class CsvHistoryExportServiceTests
{
    private readonly CsvHistoryExportService _service;

    public CsvHistoryExportServiceTests()
    {
        _service = new CsvHistoryExportService();
    }

    /// <summary>取引履歴リストが正しい CSV 文字列（ヘッダーとデータ行）に変換されることを検証します。</summary>
    [Fact]
    public void ExportShouldReturnCorrectCsvString()
    {
        // Arrange
        var timestamp = new DateTimeOffset(2026, 3, 23, 12, 0, 0, TimeSpan.FromHours(9));
        var entries = new List<TransactionEntry>
        {
            new TransactionEntry(
                timestamp,
                TransactionType.Deposit,
                1500,
                new Dictionary<DenominationKey, int>
                {
                    { new DenominationKey(1000, CurrencyCashType.Bill, "JPY"), 1 },
                    { new DenominationKey(500, CurrencyCashType.Coin, "JPY"), 1 }
                }
            ),
            new TransactionEntry(
                timestamp.AddMinutes(5),
                TransactionType.Dispense,
                -200,
                new Dictionary<DenominationKey, int>
                {
                    { new DenominationKey(100, CurrencyCashType.Coin, "JPY"), -2 }
                }
            )
        };

        // Act
        var csv = _service.Export(entries);

        // Assert
        var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.Length.ShouldBe(3); // Header + 2 entries
        
        // Header verification
        lines[0].ShouldBe("Timestamp,Type,Amount,Details");

        // Row 1 verification
        lines[1].ShouldContain("2026-03-23 12:00:00");
        lines[1].ShouldContain("Deposit");
        lines[1].ShouldContain("1500");
        lines[1].ShouldContain("JPY-B1000:1");
        lines[1].ShouldContain("JPY-C500:1");

        // Row 2 verification
        lines[2].ShouldContain("Dispense");
        lines[2].ShouldContain("-200");
        lines[2].ShouldContain("JPY-C100:-2");
    }

    /// <summary>空の取引履歴リストを渡した際に、ヘッダーのみの CSV が生成されることを検証します。</summary>
    [Fact]
    public void ExportShouldHandleEmptyList()
    {
        // Arrange
        var entries = Enumerable.Empty<TransactionEntry>();

        // Act
        var csv = _service.Export(entries);

        // Assert
        var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.Length.ShouldBe(1);
        lines[0].ShouldBe("Timestamp,Type,Amount,Details");
    }
}
