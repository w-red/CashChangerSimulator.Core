using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.UI.Cli.Services;
using Shouldly;

namespace CashChangerSimulator.Tests.Ui.Cli;

/// <summary>CliViewService の表示機能を検証するテストクラス。</summary>
public class CliViewServiceTests : CliTestBase
{
    private readonly CliViewService _viewService;

    public CliViewServiceTests() : base()
    {
        _viewService = new CliViewService(
            _mockChanger.Object,
            _mockInventory.Object,
            _mockMetadata.Object,
            _mockHistory.Object,
            _console,
            _localizer);
    }

    [Fact]
    public void StatusShouldPrintDeviceStateAndInventory()
    {
        // Arrange
        // State, DeviceEnabled, CurrencyCode, SymbolPrefix/Suffix are already setup in base.CliTestBase()
        
        var denoms = new[] { new DenominationKey(100, CurrencyCashType.Coin, "JPY") };
        _mockMetadata.Setup(x => x.SupportedDenominations).Returns(denoms);
        _mockInventory.Setup(x => x.GetCount(denoms[0])).Returns(5);
        _mockInventory.Setup(x => x.CalculateTotal("JPY")).Returns(500m);

        // Act
        _viewService.Status();

        // Assert
        var output = _console.Output;
        output.ShouldContain("Idle");
        output.ShouldContain("True");
        output.ShouldContain("100");
        output.ShouldContain("5");
        output.ShouldContain("¥500");
    }

    [Fact]
    public void HistoryShouldPrintTransactionEntries()
    {
        // Arrange
        var entries = new List<TransactionEntry>
        {
            new(DateTimeOffset.Now, TransactionType.Deposit, 1000m, new Dictionary<DenominationKey, int>())
        };
        _mockHistory.SetupGet(x => x.Entries).Returns(entries);
        _mockMetadata.SetupGet(x => x.CurrencyCode).Returns("JPY");

        // Act
        _viewService.History(10);

        // Assert
        var output = _console.Output;
        output.ShouldContain("Deposit");
        output.ShouldContain("1,000");
        output.ShouldContain("JPY");
    }
}
