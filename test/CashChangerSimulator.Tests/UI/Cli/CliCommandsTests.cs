using Cocona;
using CashChangerSimulator.Device;
using CashChangerSimulator.UI.Cli;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Configuration;
using Moq;
using Xunit;
using Microsoft.PointOfService;
using MoneyKind4Opos.Currencies.Interfaces;
using R3;
using Shouldly;
using System.IO;
using System.Globalization;
using CashChangerSimulator.Device.Services;
using Spectre.Console;
using Spectre.Console.Testing;
using Microsoft.Extensions.Localization;
using CashChangerSimulator.UI.Cli.Localization;
using System.Linq;

namespace CashChangerSimulator.Tests.Ui.Cli;

public class CliCommandsTests : IDisposable
{
    private readonly string _testI18nDir;
    private readonly Mock<SimulatorCashChanger> _mockChanger;
    private readonly Mock<Inventory> _mockInventory;
    private readonly Mock<ICurrencyMetadataProvider> _mockMetadata;
    private readonly Mock<TransactionHistory> _mockHistory;
    private readonly Mock<IScriptExecutionService> _mockScriptService;
    private readonly TestConsole _console;
    private readonly IStringLocalizer _localizer;
    private readonly CliSessionOptions _options;
    private readonly CliCommands _commands;

    public CliCommandsTests()
    {
        _testI18nDir = Path.Combine(Path.GetTempPath(), "CliCommandsI18nTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_testI18nDir);
        File.WriteAllText(Path.Combine(_testI18nDir, "en.toml"), @"
DeviceOpened = 'Device opened successfully.'
StatusHeader = 'Device Status'
StateLabel = 'State'
EnabledLabel = 'Enabled'
InventoryHeader = 'Inventory'
DenominationLabel = 'Denomination'
CountLabel = 'Count'
AmountLabel = 'Amount'
TotalCaption = 'Total'
AvailableCommands = 'Available commands'
CommandLabel = 'Command'
DescriptionLabel = 'Description'
TransactionHistoryHeader = 'Recent Transactions (up to {0})'
TimestampLabel = 'Timestamp'
TypeLabel = 'Type'
CurrencyLabel = 'Currency'
CashCountsUpdated = 'Cash counts updated'
DepositStarted = 'Depositing {0} (Async: {1})...'
DepositCompleted = 'Deposit completed.'
DepositAsyncWarning = 'Deposit started in async mode'
DepositFixed = 'Deposit fixed.'
EndDepositCompleted = 'EndDeposit completed.'
DispensedSuccess = 'Dispensed {0} successfully.'
HelpDescription = 'Show this help'
ExitDescription = 'Exit'
");
        File.WriteAllText(Path.Combine(_testI18nDir, "ja.toml"), "DeviceOpened = 'デバイスオープン'");

        _localizer = new TomlStringLocalizer(_testI18nDir);
        CultureInfo.CurrentUICulture = new CultureInfo("en-US");

        _mockChanger = new Mock<SimulatorCashChanger>();
        _mockInventory = new Mock<Inventory>();
        
        var mockConfigProvider = new Mock<ConfigurationProvider>(new object?[] { null });
        _mockMetadata = new Mock<ICurrencyMetadataProvider>();
        _mockHistory = new Mock<TransactionHistory>();
        _mockScriptService = new Mock<IScriptExecutionService>();
        _console = new TestConsole();
        _options = new CliSessionOptions();

        _commands = new CliCommands(
            _mockChanger.Object,
            _mockInventory.Object,
            _mockMetadata.Object,
            _mockHistory.Object,
            _mockScriptService.Object,
            _options,
            _console,
            _localizer);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testI18nDir))
        {
            Directory.Delete(_testI18nDir, true);
        }
    }

    [Fact]
    public void StatusShouldReflectLanguageSetting()
    {
        // Arrange
        _options.Language = "en";
        _mockChanger.Setup(x => x.State).Returns(ControlState.Idle);
        _mockChanger.Setup(x => x.DeviceEnabled).Returns(true);
        _mockMetadata.Setup(x => x.SupportedDenominations).Returns([]);
        _mockMetadata.Setup(x => x.CurrencyCode).Returns("JPY");
        _mockMetadata.Setup(x => x.SymbolPrefix).Returns(new ReactiveProperty<string>("¥"));
        _mockMetadata.Setup(x => x.SymbolSuffix).Returns(new ReactiveProperty<string>(""));

        // Act
        _commands.Status();

        // Assert
        _console.Output.ShouldContain("Device Status");
        _console.Output.ShouldContain("State");
        _console.Output.ShouldContain("Enabled");
    }

    [Fact]
    public void OpenShouldCallChangerOpenAndPrintLocalizedMessage()
    {
        // Act
        _commands.Open();

        // Assert
        _mockChanger.Verify(x => x.Open(), Times.Once);
        _console.Output.ShouldContain("Device opened successfully.");
    }

    [Fact]
    public void ReadCashCountsShouldPrintLocalizedTable()
    {
        // Arrange
        var mockCashCounts = new CashCounts(
        [
            new CashCount(CashCountType.Bill, 1000, 10),
        ], false);
        _mockChanger.Setup(x => x.ReadCashCounts()).Returns(mockCashCounts);
        _mockMetadata.Setup(x => x.SupportedDenominations).Returns([]);
        _mockMetadata.Setup(x => x.SymbolPrefix).Returns(new ReactiveProperty<string>("¥"));
        _mockMetadata.Setup(x => x.SymbolSuffix).Returns(new ReactiveProperty<string>(""));
        _mockMetadata.Setup(x => x.CurrencyCode).Returns("JPY");
        _mockInventory.Setup(x => x.CalculateTotal("JPY")).Returns(10000m);

        // Act
        _commands.ReadCashCounts();

        // Assert
        _console.Output.ShouldContain("Cash counts updated");
        _console.Output.ShouldContain("Denomination");
        _console.Output.ShouldContain("Count");
    }

    [Fact]
    public void DepositShouldRespectAsyncFlagAndPrintLocalizedMessage()
    {
        // Arrange
        _options.IsAsync = true;
        _mockChanger.Setup(x => x.State).Returns(ControlState.Idle);
        _mockChanger.Setup(x => x.DeviceEnabled).Returns(true);

        // Act
        _commands.Deposit(1000);

        // Assert
        _mockChanger.Verify(x => x.BeginDeposit(), Times.Once);
        _console.Output.ShouldContain("Depositing 1000 (Async: True)");
        _console.Output.ShouldContain("Deposit started in async mode");
    }

    [Fact]
    public void DepositShouldRespectSyncFlagAndPrintLocalizedMessage()
    {
        // Arrange
        _options.IsAsync = false;
        _mockChanger.Setup(x => x.State).Returns(ControlState.Idle);
        _mockChanger.Setup(x => x.DeviceEnabled).Returns(true);

        // Act
        _commands.Deposit(1000);

        // Assert
        _mockChanger.Verify(x => x.BeginDeposit(), Times.Once);
        _mockChanger.Verify(x => x.FixDeposit(), Times.Once);
        _console.Output.ShouldContain("Deposit completed.");
    }

    [Fact]
    public void DispenseShouldPrintLocalizedMessage()
    {
        // Act
        _commands.Dispense(500);

        // Assert
        _mockChanger.Verify(x => x.DispenseChange(500), Times.Once);
        _console.Output.ShouldContain("Dispensed 500 successfully.");
    }

    [Fact]
    public void HelpShouldPrintLocalizedHeaders()
    {
        // Arrange
        _options.Language = "en";

        // Act
        _commands.Help();

        // Assert
        _console.Output.ShouldContain("Command", Case.Insensitive);
        _console.Output.ShouldContain("open", Case.Insensitive);
        _console.Output.ShouldContain("claim", Case.Insensitive);
        _console.Output.ShouldContain("status", Case.Insensitive);
    }
}
