using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device.Virtual;
using Microsoft.PointOfService;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>DispenseController の出金制御ロジックを網羅的に検証するテストクラス。</summary>
public class DispenseControllerTests
{
    private readonly Inventory inventory;
    private readonly HardwareStatusManager hw;
    private readonly Mock<CashChangerManager> mockManager;
    private readonly Mock<IDeviceSimulator> mockSimulator;
    private readonly DispenseController controller;

    public DispenseControllerTests()
    {
        inventory = Inventory.Create();
        hw = HardwareStatusManager.Create();
        mockManager = new Mock<CashChangerManager>(inventory, new TransactionHistory(), null);
        mockSimulator = new Mock<IDeviceSimulator>();
        controller = new DispenseController(mockManager.Object, hw, mockSimulator.Object);

        // Default connected state
        hw.SetConnected(true);
    }

    /// <summary>未接続状態での出金要求時に Closed エラーが発生することを検証する。</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task DispenseChangeAsyncShouldThrowClosedWhenNotConnected()
    {
        hw.SetConnected(false);
        var ex = await Should.ThrowAsync<DeviceException>(() =>
            controller.DispenseChangeAsync(100, false)).ConfigureAwait(false);
        ex.ErrorCode.ShouldBe(DeviceErrorCode.Closed);
    }

    /// <summary>ハード故障（ジャム）発生中の出金要求時に Failure エラーが発生することを検証する。</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task DispenseChangeAsyncShouldThrowFailureWhenJammed()
    {
        hw.SetJammed(true);
        var ex = await Should.ThrowAsync<DeviceException>(() =>
            controller.DispenseChangeAsync(100, false)).ConfigureAwait(false);
        ex.ErrorCode.ShouldBe(DeviceErrorCode.Failure);
    }

    /// <summary>在庫不足時の出金要求時に OverDispense エラーが報告されることを検証する。</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task DispenseChangeAsyncShouldHandleInsufficientCash()
    {
        mockManager.Setup(m => m.Dispense(It.IsAny<decimal>(), It.IsAny<string>()))
            .Throws(new InsufficientCashException("Shortage"));

        await Should.ThrowAsync<InsufficientCashException>(() =>
            controller.DispenseChangeAsync(100, false)).ConfigureAwait(false);

        controller.LastErrorCode.ShouldBe(DeviceErrorCode.Extended);
        controller.LastErrorCodeExtended.ShouldBe((int)UposCashChangerErrorCodeExtended.OverDispense);
        controller.Status.ShouldBe(CashDispenseStatus.Error);
    }

    /// <summary>有効な金種指定での出金が正常に完了することを検証する。</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task DispenseCashAsyncShouldSucceedWithValidCounts()
    {
        var counts = new Dictionary<DenominationKey, int> { { new DenominationKey(100, CurrencyCashType.Coin), 1 } };
        await controller.DispenseCashAsync(counts, false).ConfigureAwait(false);

        mockManager.Verify(m => m.Dispense(counts), Times.Once);
        controller.LastErrorCode.ShouldBe(DeviceErrorCode.Success);
        controller.Status.ShouldBe(CashDispenseStatus.Idle);
    }

    /// <summary>出金処理中に重ねて出金要求を行うと Busy エラーが発生することを検証する。</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task DispenseChangeAsyncShouldThrowBusyWhenAlreadyProcessing()
    {
        mockSimulator.Setup(s => s.SimulateDispenseAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken t) => await Task.Delay(1000, t).ConfigureAwait(false));

        var task = controller.DispenseChangeAsync(100, true);
        await Task.Delay(50).ConfigureAwait(false);

        controller.IsBusy.ShouldBeTrue();
        await Should.ThrowAsync<DeviceException>(() =>
            controller.DispenseChangeAsync(100, false)).ConfigureAwait(false);

        controller.ClearOutput();
        await task;
    }

    /// <summary>重なり発生中の出金要求時に Failure エラーが発生することを検証する。</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task DispenseChangeAsyncShouldThrowFailureWhenOverlapped()
    {
        hw.SetOverlapped(true);
        var ex = await Should.ThrowAsync<DeviceException>(() =>
            controller.DispenseChangeAsync(100, false)).ConfigureAwait(false);
        ex.ErrorCode.ShouldBe(DeviceErrorCode.Failure);
    }

    /// <summary>ClearOutput により実行中の出金処理がキャンセルされることを検証する。</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task ClearOutputShouldCancelActiveDispense()
    {
        bool wasCanceled = false;
        var tcs = new TaskCompletionSource<bool>();
        mockSimulator.Setup(s => s.SimulateDispenseAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken t) =>
            {
                tcs.SetResult(true);
                try
                {
                    await Task.Delay(5000, t).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { wasCanceled = true;
                    throw; }
            });

        var task = controller.DispenseChangeAsync(100, true);
        await tcs.Task.ConfigureAwait(false);
        controller.ClearOutput();
        await Task.Delay(100).ConfigureAwait(false);

        controller.IsBusy.ShouldBeFalse();
        wasCanceled.ShouldBeTrue();
    }

    /// <summary>予期しない例外発生時に Failure エラーとして適切に処理されることを検証する。</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task ExecuteDispenseShouldHandleUnexpectedException()
    {
        mockManager.Setup(m => m.Dispense(It.IsAny<decimal>(), It.IsAny<string>()))
            .Throws(new InvalidOperationException("Unexpected"));

        await Should.ThrowAsync<Exception>(() =>
            controller.DispenseChangeAsync(100, false)).ConfigureAwait(false);

        controller.LastErrorCode.ShouldBe(DeviceErrorCode.Failure);
        controller.Status.ShouldBe(CashDispenseStatus.Error);
    }

    /// <summary>エラー状態から ClearOutput により正常状態に復帰できることを検証する。</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task ClearOutputShouldResetStatus()
    {
        mockManager.Setup(m => m.Dispense(It.IsAny<decimal>(), It.IsAny<string>()))
            .Throws(new Exception("Fail"));

        await Should.ThrowAsync<Exception>(() =>
            controller.DispenseChangeAsync(100, false)).ConfigureAwait(false);
        controller.Status.ShouldBe(CashDispenseStatus.Error);

        controller.ClearOutput();
        controller.Status.ShouldBe(CashDispenseStatus.Idle);
    }

    /// <summary>PosControlException 発生時にエラー詳細が正しく反映されることを検証する。</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task ExecuteDispenseShouldHandlePosControlException()
    {
        mockManager.Setup(m => m.Dispense(It.IsAny<decimal>(), It.IsAny<string>()))
            .Throws(new PosControlException("Explicit error", ErrorCode.Illegal, 123));

        await Should.ThrowAsync<Exception>(() =>
            controller.DispenseChangeAsync(100, false)).ConfigureAwait(false);

        controller.LastErrorCode.ShouldBe(DeviceErrorCode.Illegal);
        controller.LastErrorCodeExtended.ShouldBe(123);
        controller.Status.ShouldBe(CashDispenseStatus.Error);
    }

    /// <summary>オブジェクトの破棄を複数回行っても例外が発生しないことを検証する。</summary>
    [Fact]
    public void DisposeShouldNotThrow()
    {
        var tempController = new DispenseController(mockManager.Object, hw, mockSimulator.Object);
        tempController.Dispose();
        tempController.Dispose();
    }

    /// <summary>非同期モードでの例外発生が正しくハンドリングされることを検証する。</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task DispenseChangeAsyncShouldHandleBackgroundException()
    {
        mockManager.Setup(m => m.Dispense(It.IsAny<decimal>(), It.IsAny<string>()))
            .Throws(new Exception("Background fail"));

        await controller.DispenseChangeAsync(100, true).ConfigureAwait(false);
        await Task.Delay(100).ConfigureAwait(false);

        controller.Status.ShouldBe(CashDispenseStatus.Error);
    }

    /// <summary>金種指定出金においても非同期モードの例外がハンドリングされることを検証する。</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task DispenseCashAsyncShouldHandleBackgroundException()
    {
        var counts = new Dictionary<DenominationKey, int> { { new DenominationKey(100, CurrencyCashType.Coin), 1 } };
        mockManager.Setup(m => m.Dispense(It.IsAny<IReadOnlyDictionary<DenominationKey, int>>()))
            .Throws(new Exception("Background fail"));

        await controller.DispenseCashAsync(counts, true).ConfigureAwait(false);
        await Task.Delay(100).ConfigureAwait(false);

        controller.Status.ShouldBe(CashDispenseStatus.Error);
    }

    /// <summary>ExecuteDispense 内で DeviceException がキャッチされることを検証する。</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task ExecuteDispenseShouldCatchDeviceException()
    {
        mockManager.Setup(m => m.Dispense(It.IsAny<decimal>(), It.IsAny<string>()))
            .Throws(new DeviceException("Internal Jam", DeviceErrorCode.Jammed, 456));

        await Should.ThrowAsync<DeviceException>(() =>
            controller.DispenseChangeAsync(100, false)).ConfigureAwait(false);

        controller.LastErrorCode.ShouldBe(DeviceErrorCode.Jammed);
        controller.LastErrorCodeExtended.ShouldBe(456);
        controller.Status.ShouldBe(CashDispenseStatus.Error);
    }

    /// <summary>シミュレーター実行中のキャンセルが正しく処理されることを検証する。</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task ExecuteDispenseShouldHandleCancellation()
    {
        var tcs = new TaskCompletionSource<bool>();
        mockSimulator.Setup(s => s.SimulateDispenseAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken t) =>
            {
                tcs.SetResult(true);
                await Task.Delay(5000, t).ConfigureAwait(false);
            });

        var task = controller.DispenseChangeAsync(100, true);
        await tcs.Task.ConfigureAwait(false);

        controller.ClearOutput(); // Triggers cancellation

        await task;
        controller.Status.ShouldBe(CashDispenseStatus.Idle);
        controller.LastErrorCode.ShouldBe(DeviceErrorCode.Cancelled);
    }
}
