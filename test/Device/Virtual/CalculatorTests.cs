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

    [Fact]
    public void DepositCalculator_ProcessChange_WhenChangeAmountIsZero_UsesElseBranch()
    {
        // Arrange
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        _inventory.AddEscrow(key, 1);
        var depositCounts = new Dictionary<DenominationKey, int> { { key, 1 } };
        var calculator = new DepositCalculator(_mockLogger.Object, _inventory, _mockManager.Object);

        // Act
        // deposit = 1000, required = 1000 -> change = 0
        calculator.ProcessChange(1000, 1000, depositCounts);

        // Assert
        _mockManager.Verify(m => m.Deposit(It.Is<Dictionary<DenominationKey, int>>(d => d[key] == 1)), Times.Once);
        _inventory.EscrowCounts.ShouldBeEmpty();
    }

    [Fact]
    public void DepositCalculator_ProcessChange_PreferLargerDenominations()
    {
        // Arrange
        var key1000 = new DenominationKey(1000, CurrencyCashType.Bill);
        var key5000 = new DenominationKey(5000, CurrencyCashType.Bill);
        _inventory.AddEscrow(key1000, 10); // 10000
        _inventory.AddEscrow(key5000, 1);  // 5000
        var depositCounts = new Dictionary<DenominationKey, int> { { key1000, 10 }, { key5000, 1 } };
        var calculator = new DepositCalculator(_mockLogger.Object, _inventory, _mockManager.Object);

        // Act
        // deposit = 15000, required = 10000 -> change = 5000
        calculator.ProcessChange(15000, 10000, depositCounts);

        // Assert
        // Should use 1x5000 instead of 5x1000
        _mockManager.Verify(m => m.Deposit(It.Is<Dictionary<DenominationKey, int>>(d => 
            d[key5000] == 0 && d[key1000] == 10)), Times.Once);
    }

    [Fact]
    public void DepositCalculator_ProcessChange_ExactChangeFromEscrow_NoDispenseNeeded()
    {
        // Arrange
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        _inventory.AddEscrow(key, 2);
        var depositCounts = new Dictionary<DenominationKey, int> { { key, 2 } };
        var calculator = new DepositCalculator(_mockLogger.Object, _inventory, _mockManager.Object);

        // Act
        // deposit = 2000, required = 1000 -> change = 1000 (Exact match in escrow)
        calculator.ProcessChange(2000, 1000, depositCounts);

        // Assert
        _mockManager.Verify(m => m.Deposit(It.Is<Dictionary<DenominationKey, int>>(d => d[key] == 1)), Times.Once);
        _mockManager.Verify(m => m.Dispense(It.IsAny<decimal>()), Times.Never);
    }

    [Fact]
    public void DepositCalculator_ProcessChange_MultipleUseCount_CorrectRemainingChange()
    {
        // Arrange
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        _inventory.AddEscrow(key, 5);
        var depositCounts = new Dictionary<DenominationKey, int> { { key, 5 } };
        var calculator = new DepositCalculator(_mockLogger.Object, _inventory, _mockManager.Object);

        // Act
        // deposit = 5000, required = 2500 -> change = 2500
        // Should use 2x1000 from escrow, remaining 500
        calculator.ProcessChange(5000, 2500, depositCounts);

        // Assert
        _mockManager.Verify(m => m.Deposit(It.Is<Dictionary<DenominationKey, int>>(d => d[key] == 3)), Times.Once);
        _mockManager.Verify(m => m.Dispense(500m), Times.Once);
    }

    [Fact]
    public void DepositCalculator_ProcessChange_RemainingChangeAfterEscrow_CallsDispense()
    {
        // Arrange
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        _inventory.AddEscrow(key, 1);
        var depositCounts = new Dictionary<DenominationKey, int> { { key, 1 } };
        var calculator = new DepositCalculator(_mockLogger.Object, _inventory, _mockManager.Object);

        // Act
        // deposit = 1000, required = 500 -> change = 500 (No match in escrow, 1000 is too large)
        calculator.ProcessChange(1000, 500, depositCounts);

        // Assert
        _mockManager.Verify(m => m.Deposit(It.Is<Dictionary<DenominationKey, int>>(d => d[key] == 1)), Times.Once);
        _mockManager.Verify(m => m.Dispense(500m), Times.Once);
    }

    [Fact]
    public void DepositCalculator_WithNullManager_UpdatesInventoryDirectly()
    {
        // Arrange
        var calculator = new DepositCalculator(_mockLogger.Object, _inventory, null);
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        var counts = new Dictionary<DenominationKey, int> { { key, 3 } };

        // Act
        calculator.ProcessNoChange(counts);

        // Assert
        _inventory.GetCount(key).ShouldBe(3);
    }

    [Fact]
    public void DepositCalculator_ProcessChange_WithNullManager_DoesNotDispense()
    {
        // Arrange
        var calculator = new DepositCalculator(_mockLogger.Object, _inventory, null);
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        _inventory.AddEscrow(key, 1);
        var depositCounts = new Dictionary<DenominationKey, int> { { key, 1 } };

        // Act
        // change = 500. manager is null, so it should just update inventory and finish.
        calculator.ProcessChange(1000, 500, depositCounts);

        // Assert
        _inventory.GetCount(key).ShouldBe(1);
    }
}
