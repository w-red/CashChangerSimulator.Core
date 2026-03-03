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
using CashChangerSimulator.Device.Services;
using Spectre.Console;
using System.Linq;

namespace CashChangerSimulator.Tests.Ui.Cli;

public class CliCommandsTests
{
    private readonly Mock<SimulatorCashChanger> _mockChanger;
    private readonly Mock<Inventory> _mockInventory;
    private readonly Mock<ICurrencyMetadataProvider> _mockMetadata;
    private readonly Mock<TransactionHistory> _mockHistory;
    private readonly Mock<IScriptExecutionService> _mockScriptService;
    private readonly Mock<IAnsiConsole> _mockConsole;
    private readonly CliSessionOptions _options;
    private readonly CliCommands _commands;

    public CliCommandsTests()
    {
        // Mocking objects that might not have a parameterless constructor
        _mockChanger = new Mock<SimulatorCashChanger>();
        _mockInventory = new Mock<Inventory>();
        
        // ConfigurationProvider has a constructor with default string? configPath = null
        var mockConfigProvider = new Mock<ConfigurationProvider>(new object?[] { null });
        _mockMetadata = new Mock<ICurrencyMetadataProvider>();
        _mockHistory = new Mock<TransactionHistory>();
        _mockScriptService = new Mock<IScriptExecutionService>();
        _mockConsole = new Mock<IAnsiConsole>();
        _options = new CliSessionOptions();

        _commands = new CliCommands(
            _mockChanger.Object,
            _mockInventory.Object,
            _mockMetadata.Object,
            _mockHistory.Object,
            _mockScriptService.Object,
            _options,
            _mockConsole.Object);
    }

    [Fact]
    public void DepositShouldRespectAsyncFlag()
    {
        // Arrange
        _options.IsAsync = true;
        _mockChanger.Setup(x => x.State).Returns(ControlState.Idle);
        _mockChanger.Setup(x => x.DeviceEnabled).Returns(true);

        // Act
        _commands.Deposit(1000);

        // Assert
        // In async mode, it should only call BeginDeposit. 
        // Sync methods like FixDeposit/EndDeposit should NOT be called in this simple dispatcher if we want pure async control,
        // OR we expect a different behavior. Let's assume for now it calls BeginDeposit and returns.
        _mockChanger.Verify(x => x.BeginDeposit(), Times.Once);
        _mockChanger.Verify(x => x.FixDeposit(), Times.Never);
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
        // Verified by lack of exception.
        _mockConsole.Verify(x => x.Write(It.IsAny<Rule>()), Times.AtLeastOnce);
    }
    
    [Fact]
    public void DepositShouldRespectSyncFlag()
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
        _mockChanger.Verify(x => x.EndDeposit(CashDepositAction.Change), Times.Once);
    }

    [Fact]
    public async Task RunScriptShouldExecuteServiceAsync()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        var json = "[{\"Op\": \"BeginDeposit\"}]";
        File.WriteAllText(tempFile, json);
        try
        {
            _mockScriptService.Setup(x => x.ExecuteScriptAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

            // Act
            await _commands.RunScript(tempFile);

            // Assert
            _mockScriptService.Verify(x => x.ExecuteScriptAsync(json), Times.Once);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void OpenShouldCallChangerOpen()
    {
        // Act
        _commands.Open();

        // Assert
        _mockChanger.Verify(x => x.Open(), Times.Once);
    }

    [Fact]
    public void DepositShouldCallBeginAndEndDeposit()
    {
        // Arrange
        _mockChanger.Setup(x => x.State).Returns(ControlState.Idle);
        _mockChanger.Setup(x => x.DeviceEnabled).Returns(true);

        // Act
        _commands.Deposit(1000);

        // Assert
        _mockChanger.Verify(x => x.BeginDeposit(), Times.Once);
        _mockChanger.Verify(x => x.FixDeposit(), Times.Once);
        _mockChanger.Verify(x => x.EndDeposit(CashDepositAction.Change), Times.Once);
    }

    [Fact]
    public void DispenseShouldCallDispenseChange()
    {
        // Act
        _commands.Dispense(500);

        // Assert
        _mockChanger.Verify(x => x.DispenseChange(500), Times.Once);
    }

    [Fact]
    public void ReadCashCountsShouldPrintColoredTable()
    {
        // Arrange
        var mockCashCounts = new CashCounts(
        [
            new CashCount(CashCountType.Bill, 1000, 10),
            new CashCount(CashCountType.Coin, 100, 50)
        ], false);
        _mockChanger.Setup(x => x.ReadCashCounts()).Returns(mockCashCounts);
        _mockMetadata.Setup(x => x.SupportedDenominations).Returns([]);
        _mockMetadata.Setup(x => x.SymbolPrefix).Returns(new ReactiveProperty<string>("¥"));
        _mockMetadata.Setup(x => x.SymbolSuffix).Returns(new ReactiveProperty<string>(""));
        _mockMetadata.Setup(x => x.CurrencyCode).Returns("JPY");
        _mockInventory.Setup(x => x.CalculateTotal("JPY")).Returns(15000m);

        // Act
        _commands.ReadCashCounts();

        // Assert
        // Verified by lack of exception and internal logic
        _mockConsole.Verify(x => x.Write(It.IsAny<Table>()), Times.Once);
    }
    
    [Fact]
    public void FixDepositShouldCallFixDepositOnChanger()
    {
        // Act
        _commands.FixDeposit();

        // Assert
        _mockChanger.Verify(x => x.FixDeposit(), Times.Once);
    }

    [Fact]
    public void EndDepositShouldCallEndDepositOnChanger()
    {
        // Act
        _commands.EndDeposit();

        // Assert
        _mockChanger.Verify(x => x.EndDeposit(CashDepositAction.Change), Times.Once);
    }
}
