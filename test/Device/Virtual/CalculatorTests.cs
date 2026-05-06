using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device.Virtual;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Tests.Device.Virtual;

public class CalculatorTests
{
    private readonly Inventory _inventory;
    private readonly Mock<CashChangerManager> _mockManager;
    private readonly Mock<ILogger> _mockLogger;

    public CalculatorTests()
    {
        _inventory = Inventory.Create();
        // CashChangerManager requires a lot of dependencies, so we mock it.
        // But the calculators use it for Deposit/Dispense methods.
        _mockManager = new Mock<CashChangerManager>(
            _inventory,
            new TransactionHistory(),
            null! // configProvider
        );
        _mockLogger = new Mock<ILogger>();
    }

    [Fact]
    public void DepositCalculator_ProcessRepay_ClearsEscrow()
    {
        // Arrange
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        _inventory.AddEscrow(key, 5);
        var calculator = new DepositCalculator(_mockLogger.Object, _inventory, _mockManager.Object);

        // Act
        calculator.ProcessRepay();

        // Assert
        _inventory.EscrowCounts.ShouldBeEmpty();
    }

    [Fact]
    public void DepositCalculator_ProcessNoChange_UpdatesInventoryAndManager()
    {
        // Arrange
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        var counts = new Dictionary<DenominationKey, int> { { key, 5 } };
        var calculator = new DepositCalculator(_mockLogger.Object, _inventory, _mockManager.Object);

        // Act
        calculator.ProcessNoChange(counts);

        // Assert
        _mockManager.Verify(m => m.Deposit(It.Is<Dictionary<DenominationKey, int>>(d => d[key] == 5)), Times.Once);
        _inventory.EscrowCounts.ShouldBeEmpty();
    }

    [Fact]
    public void DepositCalculator_ProcessChange_HandlesChangeFromEscrow()
    {
        // Arrange
        var key1000 = new DenominationKey(1000, CurrencyCashType.Bill);
        var key5000 = new DenominationKey(5000, CurrencyCashType.Bill);
        
        _inventory.AddEscrow(key1000, 2);
        _inventory.AddEscrow(key5000, 1);
        
        var depositCounts = new Dictionary<DenominationKey, int>
        {
            { key1000, 2 },
            { key5000, 1 }
        };

        var calculator = new DepositCalculator(_mockLogger.Object, _inventory, _mockManager.Object);

        // depositAmount = 7000, requiredAmount = 1500 -> changeAmount = 5500
        // Expected behavior: use 1x5000 and 0x1000 from escrow if possible (descending order).
        // Wait, the logic says:
        // int useCount = (int)Math.Min(countInEscrow, Math.Floor(remainingChange / key.Value));
        // remainingChange = 5500. key=5000, countInEscrow=1. useCount = min(1, 1) = 1.
        // remainingChange = 5500 - 5000 = 500.
        // next key=1000, countInEscrow=2. useCount = min(2, 0) = 0.
        // remainingChange = 500.
        // Then manager.Dispense(500) called.

        // Act
        calculator.ProcessChange(7000, 1500, depositCounts);

        // Assert
        _mockManager.Verify(m => m.Deposit(It.Is<Dictionary<DenominationKey, int>>(d => 
            d[key5000] == 0 && d[key1000] == 2)), Times.Once);
        _mockManager.Verify(m => m.Dispense(500m), Times.Once);
        _inventory.EscrowCounts.ShouldBeEmpty();
    }

    [Fact]
    public void DispenseCalculator_ProcessDispense_UpdatesManagerAndHardware()
    {
        // Arrange
        var hardwareStatusManager = HardwareStatusManager.Create();
        var calculator = new DispenseCalculator(_mockManager.Object, hardwareStatusManager);
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        var counts = new Dictionary<DenominationKey, int> { { key, 3 } };

        // Act
        calculator.ProcessDispense(counts, isRepay: false);

        // Assert
        _mockManager.Verify(m => m.Dispense(counts), Times.Once);
        hardwareStatusManager.State.GetExitPortCounts(ExitPort.Normal)[key].ShouldBe(3);
    }

    [Fact]
    public void DispenseCalculator_ProcessDispense_Repay_UsesCollectionPort()
    {
        // Arrange
        var hardwareStatusManager = HardwareStatusManager.Create();
        var calculator = new DispenseCalculator(_mockManager.Object, hardwareStatusManager);
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        var counts = new Dictionary<DenominationKey, int> { { key, 3 } };

        // Act
        calculator.ProcessDispense(counts, isRepay: true);

        // Assert
        _mockManager.Verify(m => m.Dispense(counts), Times.Once);
        hardwareStatusManager.State.GetExitPortCounts(ExitPort.Collection)[key].ShouldBe(3);
    }
}
