using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using Moq;
using Shouldly;

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
        var key = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        inventory.SetCount(key, 10);
        var manager = new CashChangerManager(inventory, new TransactionHistory(), new ChangeCalculator());
        var hw = new HardwareStatusManager();
        hw.SetConnected(true);
        var controller = new DispenseController(manager, hw, new HardwareSimulator(new ConfigurationProvider()));

        ErrorCode resultCode = ErrorCode.Failure;

        // Act (synchronous mode so we can assert final state after await)
        await controller.DispenseChangeAsync(1000, false, (code, ex) => resultCode = code);

        // Assert
        controller.Status.ShouldBe(CashDispenseStatus.Idle);
        controller.IsBusy.ShouldBeFalse();
        resultCode.ShouldBe(ErrorCode.Success);
        inventory.GetCount(key).ShouldBe(9);
    }

    /// <summary>ビジー状態での払い出し呼び出しが例外をスローすることを検証します。</summary>
    [Fact]
    public async Task DispenseChangeAsyncShouldThrowIfBusy()
    {
        // Arrange
        var inventory = new Inventory();
        var manager = new CashChangerManager(inventory, new TransactionHistory(), new ChangeCalculator());
        var hw = new HardwareStatusManager();
        hw.SetConnected(true);
        var controller = new DispenseController(manager, hw, new HardwareSimulator(new ConfigurationProvider()));

        // Act & Assert
        // Start first dispense (async mode keeps it in BUSY)
        _ = controller.DispenseChangeAsync(1000, true, IgnoreDispenseResult);

        // Wait briefly for status to transition
        await Task.Delay(TestTimingConstants.StartupCheckDelayMs, TestContext.Current.CancellationToken);

        // Second call should throw
        await Should.ThrowAsync<PosControlException>(() => controller.DispenseChangeAsync(1000, false, IgnoreDispenseResult));
    }

    /// <summary>払い出し操作中にシミュレーターが呼び出されることを検証します。</summary>
    [Fact]
    public async Task DispenseChangeAsyncShouldCallSimulator()
    {
        // Arrange
        var inventory = new Inventory();
        var key = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        inventory.SetCount(key, 10);
        var manager = new CashChangerManager(inventory, new TransactionHistory(), new ChangeCalculator());
        var mockSimulator = new Mock<IDeviceSimulator>();

        var hw = new HardwareStatusManager();
        hw.SetConnected(true);
        var controller = new DispenseController(manager, hw, mockSimulator.Object);

        // Act
        await controller.DispenseChangeAsync(1000, false, IgnoreDispenseResult);

        // Assert
        mockSimulator.Verify(s => s.SimulateDispenseAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
