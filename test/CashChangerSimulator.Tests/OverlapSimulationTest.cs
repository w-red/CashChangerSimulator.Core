using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Tests;

public class OverlapSimulationTest
{
    [Fact]
    public void TrackBulkDeposit_ShouldSimulateOverlap_WhenFailureRateIsHigh()
    {
        // Arrange
        var inventory = new Inventory();
        var history = new TransactionHistory();
        var manager = new CashChangerManager(inventory, history);
        var simSettings = new SimulationSettings { RandomErrorsEnabled = true, ValidationFailureRate = 100 }; // 100% failure
        var hardwareManager = new HardwareStatusManager();
        var controller = new DepositController(inventory, simSettings, hardwareManager);

        var key = new DenominationKey(1000, MoneyKind4Opos.Currencies.Interfaces.CashType.Bill);
        var counts = new Dictionary<DenominationKey, int> { { key, 1 } };

        // Act
        controller.BeginDeposit();
        controller.TrackBulkDeposit(counts);

        // Assert
        Assert.True(hardwareManager.IsOverlapped.Value);
    }

    [Fact]
    public void FixDeposit_ShouldSucceed_ButEndDepositNoChange_ShouldThrow_WhenOverlapped()
    {
        // Arrange
        var inventory = new Inventory();
        var history = new TransactionHistory();
        var manager = new CashChangerManager(inventory, history);
        var simSettings = new SimulationSettings { RandomErrorsEnabled = true, ValidationFailureRate = 100 };
        var hardwareManager = new HardwareStatusManager();
        var controller = new DepositController(inventory, simSettings, hardwareManager);

        var key = new DenominationKey(1000, MoneyKind4Opos.Currencies.Interfaces.CashType.Bill);
        var counts = new Dictionary<DenominationKey, int> { { key, 1 } };

        controller.BeginDeposit();
        controller.TrackBulkDeposit(counts);

        // Act & Assert
        // Fix should now succeed to allow Repay flow
        controller.FixDeposit();
        Assert.True(controller.IsFixed);

        // EndDeposit(NoChange) should throw if still overlapped
        Assert.Throws<PosControlException>(() => controller.EndDeposit(CashDepositAction.NoChange));
        
        // EndDeposit(Repay) should succeed and clear error
        controller.EndDeposit(CashDepositAction.Repay);
        Assert.False(hardwareManager.IsOverlapped.Value);
    }

    [Fact]
    public void BeginDeposit_ShouldClearOverlapStatus()
    {
        // Arrange
        var hardwareManager = new HardwareStatusManager();
        hardwareManager.SetOverlapped(true);
        
        var inventory = new Inventory();
        var history = new TransactionHistory();
        var manager = new CashChangerManager(inventory, history);
        var controller = new DepositController(inventory, new SimulationSettings(), hardwareManager);

        // Act
        controller.BeginDeposit();

        // Assert
        Assert.False(hardwareManager.IsOverlapped.Value);
    }
}
