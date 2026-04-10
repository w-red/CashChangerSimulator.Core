using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device.Virtual;
using Shouldly;

namespace CashChangerSimulator.Tests.Device.Virtual;

/// <summary>重なりエラー（Overlap Error）のシミュレーションを検証するテストクラス。</summary>
public class OverlapSimulationTests : DeviceTestBase
{
    private readonly DepositController controller;

    public OverlapSimulationTests()
    {
        controller = new DepositController(Inventory, StatusManager);
        StatusManager.Input.IsConnected.Value = true;
    }

    /// <summary>重なり発生時に FixDeposit は成功するが、EndDeposit(NoChange) は失敗することを検証する。</summary>
    [Fact]
    public void FixDepositShouldSucceedButEndDepositNoChangeShouldThrowWhenOverlapped()
    {
        // Arrange
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        var counts = new Dictionary<DenominationKey, int> { { key, 1 } };

        controller.BeginDeposit();
        controller.TrackBulkDeposit(counts);

        // Manually set overlap (previously done via random error simulation)
        StatusManager.Input.IsOverlapped.Value = true;

        // Act & Assert
        // Fix should succeed to allow Repay flow
        controller.FixDeposit();
        controller.IsFixed.ShouldBeTrue();

        // EndDeposit(NoChange) should throw if overlapped
        Should.Throw<DeviceException>(() => controller.EndDeposit(DepositAction.NoChange));

        // EndDeposit(Repay) should succeed, but does not auto-clear hardware overlap
        controller.EndDeposit(DepositAction.Repay);
        StatusManager.IsOverlapped.CurrentValue.ShouldBeTrue();
    }

    /// <summary>重なり発生中に入金を開始しようとすると例外が発生することを検証する。</summary>
    [Fact]
    public void BeginDepositShouldThrowWhenOverlapped()
    {
        // Arrange
        StatusManager.Input.IsOverlapped.Value = true;

        // Act & Assert
        Should.Throw<DeviceException>(() => controller.BeginDeposit());
    }
}
