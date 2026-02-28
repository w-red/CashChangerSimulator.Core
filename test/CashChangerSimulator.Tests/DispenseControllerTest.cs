using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using MoneyKind4Opos.Currencies.Interfaces;
using Moq;

namespace CashChangerSimulator.Tests;

/// <summary>DispenseController の動作を検証するテストクラス。</summary>
public class DispenseControllerTest
{
    /// <summary>ディスペンス結果を無視するコールバック。</summary>
    private static void IgnoreDispenseResult(ErrorCode code, int codeEx) { }

    /// <summary>同期的な払い出し操作でステータスが遷移することを検証します。</summary>
    [Fact]
    public async Task DispenseChangeAsyncShouldTransitionToBusyAndBackToIdle()
    {
        // Arrange
        var inventory = new Inventory();
        var key = new DenominationKey(1000, CashType.Bill, "JPY");
        inventory.SetCount(key, 10);
        var manager = new CashChangerManager(inventory, new TransactionHistory(), new ChangeCalculator());
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

    /// <summary>ビジー状態での払い出し呼び出しが例外をスローすることを検証します。</summary>
    [Fact]
    public async Task DispenseChangeAsyncShouldThrowIfBusy()
    {
        // Arrange
        var inventory = new Inventory();
        var manager = new CashChangerManager(inventory, new TransactionHistory(), new ChangeCalculator());
        var controller = new DispenseController(manager);

        // Act & Assert
        // Start first dispense (async mode keeps it in BUSY)
        _ = controller.DispenseChangeAsync(1000, true, IgnoreDispenseResult);

        // Wait briefly for status to transition
        await Task.Delay(TestTimingConstants.StartupCheckDelayMs, TestContext.Current.CancellationToken);

        // Second call should throw
        await Assert.ThrowsAsync<PosControlException>(() => controller.DispenseChangeAsync(1000, false, IgnoreDispenseResult));
    }

    /// <summary>払い出し操作中にシミュレーターが呼び出されることを検証します。</summary>
    [Fact]
    public async Task DispenseChangeAsyncShouldCallSimulator()
    {
        // Arrange
        var inventory = new Inventory();
        var key = new DenominationKey(1000, CashType.Bill, "JPY");
        inventory.SetCount(key, 10);
        var manager = new CashChangerManager(inventory, new TransactionHistory(), new ChangeCalculator());
        var mockSimulator = new Mock<IDeviceSimulator>();
        
        var controller = new DispenseController(manager, null, mockSimulator.Object);

        // Act
        await controller.DispenseChangeAsync(1000, false, IgnoreDispenseResult);

        // Assert
        mockSimulator.Verify(s => s.SimulateDispenseAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
