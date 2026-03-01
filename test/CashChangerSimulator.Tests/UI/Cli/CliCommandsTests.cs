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
using Shouldly;

namespace CashChangerSimulator.Tests.Ui.Cli;

public class CliCommandsTests
{
    private readonly Mock<SimulatorCashChanger> _mockChanger;
    private readonly Mock<Inventory> _mockInventory;
    private readonly Mock<CurrencyMetadataProvider> _mockMetadata;
    private readonly Mock<TransactionHistory> _mockHistory;
    private readonly CliCommands _commands;

    public CliCommandsTests()
    {
        // Mocking objects that might not have a parameterless constructor
        _mockChanger = new Mock<SimulatorCashChanger>();
        _mockInventory = new Mock<Inventory>();
        
        // ConfigurationProvider has a constructor with default string? configPath = null
        var mockConfigProvider = new Mock<ConfigurationProvider>(new object?[] { null });
        _mockMetadata = new Mock<CurrencyMetadataProvider>(mockConfigProvider.Object);
        _mockHistory = new Mock<TransactionHistory>();

        _commands = new CliCommands(
            _mockChanger.Object,
            _mockInventory.Object,
            _mockMetadata.Object,
            _mockHistory.Object);
    }

    [Fact]
    public void Open_ShouldCallChangerOpen()
    {
        // Act
        _commands.Open();

        // Assert
        _mockChanger.Verify(x => x.Open(), Times.Once);
    }

    [Fact]
    public void Deposit_ShouldCallBeginAndEndDeposit()
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
    public void Dispense_ShouldCallDispenseChange()
    {
        // Act
        _commands.Dispense(500);

        // Assert
        _mockChanger.Verify(x => x.DispenseChange(500), Times.Once);
    }
}
