using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device.Virtual;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>重なりエラー（Overlap Error）のシミュレーションを検証するテストクラス。</summary>
public class OverlapSimulationTests
{
    /// <summary>重なり発生時に FixDeposit は成功するが、EndDeposit(NoChange) は失敗することを検証する。</summary>
    [Fact]
    public void FixDepositShouldSucceedButEndDepositNoChangeShouldThrowWhenOverlapped()
    {
        // Arrange
        var inventory = Inventory.Create();
        var hardwareManager = HardwareStatusManager.Create();
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
        Should.Throw<DeviceException>(() => controller.EndDeposit(DepositAction.NoChange));

        // EndDeposit(Repay) should succeed, but does not auto-clear hardware overlap
        controller.EndDeposit(DepositAction.Repay);
        hardwareManager.IsOverlapped.Value.ShouldBeTrue();
    }

    /// <summary>重なり発生中に入金を開始しようとすると例外が発生することを検証する。</summary>
    [Fact]
    public void BeginDepositShouldThrowWhenOverlapped()
    {
        // Arrange
        var hardwareManager = HardwareStatusManager.Create();
        hardwareManager.SetOverlapped(true);

        var inventory = Inventory.Create();
        var controller = new DepositController(inventory, hardwareManager);

        hardwareManager.SetConnected(true);

        // Act & Assert
        Should.Throw<DeviceException>(() => controller.BeginDeposit());
    }
}
