using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using Shouldly;

namespace CashChangerSimulator.Tests;

/// <summary>重なりエラー（Overlap Error）のシミュレーションを検証するテストクラス。</summary>
public class OverlapSimulationTest
{
    /// <summary>重なりエラー発生時に FixDeposit は成功するが、EndDeposit(NoChange) は失敗することを検証する。</summary>
    [Fact]
    public void FixDepositShouldSucceedButEndDepositNoChangeShouldThrowWhenOverlapped()
    {
        // Arrange
        var inventory = new Inventory();
        var hardwareManager = new HardwareStatusManager();
        var controller = new DepositController(inventory, hardwareManager);

        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        var counts = new Dictionary<DenominationKey, int> { { key, 1 } };

        hardwareManager.SetConnected(true);
        controller.BeginDeposit();
        controller.TrackBulkDeposit(counts);

        // Manually set overlap (previously done via random error simulation)
        hardwareManager.SetOverlapped(true);

        // Act & Assert
        // Fix should succeed to allow Repay flow
        controller.FixDeposit();
        controller.IsFixed.ShouldBeTrue();

        // EndDeposit(NoChange) should throw if overlapped
        Should.Throw<PosControlException>(() => controller.EndDeposit(CashDepositAction.NoChange));

        // EndDeposit(Repay) should succeed, but does not auto-clear hardware overlap
        controller.EndDeposit(CashDepositAction.Repay);
        hardwareManager.IsOverlapped.Value.ShouldBeTrue();
    }

    /// <summary>BeginDeposit を呼び出すと重なりエラーの場合は例外が発生することを検証する。</summary>
    [Fact]
    public void BeginDepositThrowsWhenOverlapped()
    {
        // Arrange
        var hardwareManager = new HardwareStatusManager();
        hardwareManager.SetOverlapped(true);

        var inventory = new Inventory();
        var controller = new DepositController(inventory, hardwareManager);

        hardwareManager.SetConnected(true);
        // Act & Assert
        Should.Throw<PosControlException>(() => controller.BeginDeposit());
    }
}
