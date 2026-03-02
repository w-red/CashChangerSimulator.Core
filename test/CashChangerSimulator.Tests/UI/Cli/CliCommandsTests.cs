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

namespace CashChangerSimulator.Tests.Ui.Cli;

public class CliCommandsTests
{
    private readonly Mock<SimulatorCashChanger> _mockChanger;
    private readonly Mock<Inventory> _mockInventory;
    private readonly Mock<ICurrencyMetadataProvider> _mockMetadata;
    private readonly Mock<TransactionHistory> _mockHistory;
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

        _commands = new CliCommands(
            _mockChanger.Object,
            _mockInventory.Object,
            _mockMetadata.Object,
            _mockHistory.Object);
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
    public void ReadCashCountsShouldPrintTable()
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
        _mockChanger.Verify(x => x.ReadCashCounts(), Times.Once);
    }
}
