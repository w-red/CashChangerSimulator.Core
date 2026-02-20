using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Tests;

/// <summary>重なりエラー（Overlap Error）のシミュレーションを検証するテストクラス。</summary>
public class OverlapSimulationTest
{
    [Fact]
    public void FixDeposit_ShouldSucceed_ButEndDepositNoChange_ShouldThrow_WhenOverlapped()
    {
        // Arrange
        var inventory = new Inventory();
        var hardwareManager = new HardwareStatusManager();
        var controller = new DepositController(inventory, new SimulationSettings(), hardwareManager);

        var key = new DenominationKey(1000, MoneyKind4Opos.Currencies.Interfaces.CashType.Bill);
        var counts = new Dictionary<DenominationKey, int> { { key, 1 } };

        controller.BeginDeposit();
        controller.TrackBulkDeposit(counts);

        // Manually set overlap (previously done via random error simulation)
        hardwareManager.SetOverlapped(true);

        // Act & Assert
        // Fix should succeed to allow Repay flow
        controller.FixDeposit();
        Assert.True(controller.IsFixed);

        // EndDeposit(NoChange) should throw if overlapped
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
        var controller = new DepositController(inventory, new SimulationSettings(), hardwareManager);

        // Act
        controller.BeginDeposit();

        // Assert
        Assert.False(hardwareManager.IsOverlapped.Value);
    }
}
