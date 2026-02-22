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
        var controller = new DispenseController(manager);

        ErrorCode resultCode = ErrorCode.Failure;

        // Act (synchronous mode so we can assert final state after await)
        await controller.DispenseChangeAsync(1000, false, (code, ex) => resultCode = code);

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
        var controller = new DispenseController(manager);

        // Act & Assert
        // Start first dispense (async mode keeps it in BUSY)
        _ = controller.DispenseChangeAsync(1000, true, IgnoreDispenseResult);

        // Wait briefly for status to transition
        await Task.Delay(TestTimingConstants.StartupCheckDelayMs, TestContext.Current.CancellationToken);

        // Second call should throw
        await Assert.ThrowsAsync<PosControlException>(() => controller.DispenseChangeAsync(1000, false, IgnoreDispenseResult));
    }
}
