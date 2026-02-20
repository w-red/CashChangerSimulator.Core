using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using MoneyKind4Opos.Currencies.Interfaces;

namespace CashChangerSimulator.Tests;

/// <summary>DispenseController の動作を検証するテストクラス。</summary>
public class DispenseControllerTest
{
    /// <summary>ディスペンス結果を無視するコールバック。</summary>
    private static void IgnoreDispenseResult(ErrorCode code, int codeEx) { }

    [Fact]
    public async Task DispenseChangeAsync_ShouldTransitionToBusyAndBackToIdle()
    {
        // Arrange
        var inventory = new Inventory();
        var key = new DenominationKey(1000, CashType.Bill, "JPY");
        inventory.SetCount(key, 10);
        var manager = new CashChangerManager(inventory, new TransactionHistory());

        // Enable delay to capture Busy state
        var config = new SimulationSettings { DelayEnabled = true, MinDelayMs = 100, MaxDelayMs = 200 };
        var controller = new DispenseController(manager, config);

        ErrorCode resultCode = ErrorCode.Failure;

        // Act
        // Use asyncMode: true to check status immediately
        var task = controller.DispenseChangeAsync(1000, true, (code, ex) => resultCode = code);

        Assert.Equal(CashDispenseStatus.Busy, controller.Status);
        Assert.True(controller.IsBusy);

        // Wait for completion (in test we don't await the returned Task since it's discarded in asyncMode internally, 
        // but we can poll or wait for the resultCode if we had a way, 
        // OR just use a synchronous call with delay and check status from another thread).
        // Actually, let's use a synchronous call with a large delay and Task.Run it here.

        await Task.Delay(TestTimingConstants.CompletionWaitMs, TestContext.Current.CancellationToken); // Wait for simulation to finish

        // Assert
        Assert.Equal(CashDispenseStatus.Idle, controller.Status);
        Assert.False(controller.IsBusy);
        Assert.Equal(ErrorCode.Success, resultCode);
        Assert.Equal(9, inventory.GetCount(key));
    }

    [Fact]
    public async Task DispenseChangeAsync_ShouldThrowIfBusy()
    {
        // Arrange
        var inventory = new Inventory();
        var manager = new CashChangerManager(inventory, new TransactionHistory());
        var config = new SimulationSettings { DelayEnabled = true, MinDelayMs = 500 };
        var controller = new DispenseController(manager, config);

        // Act & Assert
        // Start first dispense (async)
        _ = controller.DispenseChangeAsync(1000, true, IgnoreDispenseResult);

        // Wait a bit to ensure it started
        await Task.Delay(TestTimingConstants.StartupCheckDelayMs, TestContext.Current.CancellationToken);

        // Second call should throw
        await Assert.ThrowsAsync<PosControlException>(() => controller.DispenseChangeAsync(1000, false, IgnoreDispenseResult));
    }
}
