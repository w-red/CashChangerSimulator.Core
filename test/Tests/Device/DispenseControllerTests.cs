using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Virtual;
using Microsoft.PointOfService;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>出金制御クラス（DispenseController）の基本動作とエラー系を検証するテストクラス。.</summary>
public class DispenseControllerTests
{
    private readonly Inventory inventory;
    private readonly HardwareStatusManager hw;
    private readonly Mock<CashChangerManager> mockManager;
    private readonly Mock<IDeviceSimulator> mockSimulator;
    private readonly DispenseController controller;

    public DispenseControllerTests()
    {
        inventory = new Inventory();
        hw = new HardwareStatusManager();
        mockManager = new Mock<CashChangerManager>(inventory, new TransactionHistory(), null);
        mockSimulator = new Mock<IDeviceSimulator>();
        controller = new DispenseController(mockManager.Object, hw, mockSimulator.Object);
        controller = new DispenseController(mockManager.Object, hw, mockSimulator.Object);

        // Default connected state
        hw.SetConnected(true);
    }

    /// <summary>オフライン（未接続）状態で出金を試みた場合に E_CLOSED がスローされることを検証します。.</summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous unit test.</placeholder></returns>
    [Fact]
    public async Task DispenseChangeAsyncShouldThrowClosedWhenNotConnected()
    {
        // Arrange
        hw.SetConnected(false);

        // Act & Assert
        var ex = await Should.ThrowAsync<DeviceException>(() =>
            controller.DispenseChangeAsync((int)100, false)).ConfigureAwait(false);
        ex.ErrorCode.ShouldBe(DeviceErrorCode.Closed);
    }

    /// <summary>ハードウェア障害（ジャム）が発生している状態で出金を試みた場合に E_FAILURE がスローされることを検証します。.</summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous unit test.</placeholder></returns>
    [Fact]
    public async Task DispenseChangeAsyncShouldThrowFailureWhenJammed()
    {
        // Arrange
        hw.SetJammed(true);

        // Act & Assert
        var ex = await Should.ThrowAsync<DeviceException>(() =>
            controller.DispenseChangeAsync((int)100, false)).ConfigureAwait(false);
        ex.ErrorCode.ShouldBe(DeviceErrorCode.Failure);
    }

    /// <summary>在庫不足（InsufficientCash）時に E_EXT (OverDispense) が正しく報告されることを検証します。.</summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous unit test.</placeholder></returns>
    [Fact]
    public async Task DispenseChangeAsyncShouldHandleInsufficientCash()
    {
        // Arrange
        mockManager.Setup(m => m.Dispense(It.IsAny<decimal>(), It.IsAny<string>()))
            .Throws(new InsufficientCashException("Shortage"));

        // Act
        await controller.DispenseChangeAsync((int)100, false).ConfigureAwait(false);

        // Assert
        controller.LastErrorCode.ShouldBe(DeviceErrorCode.Extended);
        controller.LastErrorCodeExtended.ShouldBe((int)UposCashChangerErrorCodeExtended.OverDispense);
        controller.Status.ShouldBe(CashDispenseStatus.Error);
    }

    /// <summary>指定された金種構成での出金が正常に完了することを検証します。.</summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous unit test.</placeholder></returns>
    [Fact]
    public async Task DispenseCashAsyncShouldSucceedWithValidCounts()
    {
        // Arrange
        var counts = new Dictionary<DenominationKey, int>
        {
            { new DenominationKey(100, CurrencyCashType.Coin), 1 }
        };

        // Act
        await controller.DispenseCashAsync((IReadOnlyDictionary<DenominationKey, int>)counts, false).ConfigureAwait(false);

        // Assert
        mockManager.Verify(m => m.Dispense(counts), Times.Once);
        controller.LastErrorCode.ShouldBe(DeviceErrorCode.Success);
        controller.Status.ShouldBe(CashDispenseStatus.Idle);
    }

    /// <summary>既に別の出金処理が進行中の場合に E_BUSY がスローされることを検証します。.</summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous unit test.</placeholder></returns>
    [Fact]
    public async Task DispenseChangeAsyncShouldThrowBusyWhenAlreadyProcessing()
    {
        // Arrange
        mockSimulator.Setup(s => s.SimulateDispenseAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken t) => await Task.Delay(1000, t).ConfigureAwait(false));

        _ = controller.DispenseChangeAsync((int)100, true);
        await Task.Delay(50, TestContext.Current.CancellationToken).ConfigureAwait(false);

        // Act & Assert
        controller.IsBusy.ShouldBeTrue();
        await Should.ThrowAsync<DeviceException>(() =>
            controller.DispenseChangeAsync((int)100, false)).ConfigureAwait(false);
    }

    /// <summary>オーバーラップ処理中に新たな出金要求が来た場合に E_FAILURE がスローされることを検証します。.</summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous unit test.</placeholder></returns>
    [Fact]
    public async Task DispenseChangeAsyncShouldThrowFailureWhenOverlapped()
    {
        // Arrange
        hw.SetOverlapped(true);

        // Act & Assert
        var ex = await Should.ThrowAsync<DeviceException>(() =>
            controller.DispenseChangeAsync((int)100, false)).ConfigureAwait(false);
        ex.ErrorCode.ShouldBe(DeviceErrorCode.Failure);
    }

    /// <summary>ClearOutput 呼び出しにより、実行中の出金処理がキャンセルされることを検証します。.</summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous unit test.</placeholder></returns>
    [Fact]
    public async Task ClearOutputShouldCancelActiveDispense()
    {
        // Arrange
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
                catch (OperationCanceledException)
                {
                    wasCanceled = true;
                    throw;
                }
            });

        _ = controller.DispenseChangeAsync((int)100, true);

        await tcs.Task.ConfigureAwait(false);

        // Act
        controller.ClearOutput();
        await Task.Delay(100, TestContext.Current.CancellationToken).ConfigureAwait(false);

        // Assert
        controller.IsBusy.ShouldBeFalse();
        wasCanceled.ShouldBeTrue();
    }

    /// <summary>未予期な例外が発生した場合に E_FAILURE として適切にハンドリングされることを検証します。.</summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous unit test.</placeholder></returns>
    [Fact]
    public async Task ExecuteDispenseShouldHandleUnexpectedException()
    {
        // Arrange
        mockManager.Setup(m => m.Dispense(It.IsAny<decimal>(), It.IsAny<string>()))
            .Throws(new InvalidOperationException("Unexpected"));

        // Act
        await controller.DispenseChangeAsync((int)100, false).ConfigureAwait(false);

        // Assert
        controller.LastErrorCode.ShouldBe(DeviceErrorCode.Failure);
        controller.Status.ShouldBe(CashDispenseStatus.Error);
    }

    /// <summary>エラー状態から ClearOutput により Idle 状態に復帰できることを検証します。.</summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous unit test.</placeholder></returns>
    [Fact]
    public async Task ClearOutputShouldResetStatus()
    {
        // Arrange
        mockManager.Setup(m => m.Dispense(It.IsAny<decimal>(), It.IsAny<string>()))
            .Throws(new Exception("Fail"));

        await controller.DispenseChangeAsync((int)100, false).ConfigureAwait(false);
        controller.Status.ShouldBe(CashDispenseStatus.Error);

        // Act
        controller.ClearOutput();

        // Assert
        controller.Status.ShouldBe(CashDispenseStatus.Idle);
    }

    /// <summary>明示的な PosControlException が発生した場合に、そのエラーコードが正しく反映されることを検証します。.</summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous unit test.</placeholder></returns>
    [Fact]
    public async Task ExecuteDispenseShouldHandlePosControlException()
    {
        // Arrange
        mockManager.Setup(m => m.Dispense(It.IsAny<decimal>(), It.IsAny<string>()))
            .Throws(new PosControlException("Explicit error", ErrorCode.Illegal, 123));

        // Act
        await controller.DispenseChangeAsync((int)100, false).ConfigureAwait(false);

        // Assert
        controller.LastErrorCode.ShouldBe(DeviceErrorCode.Illegal);
        controller.LastErrorCodeExtended.ShouldBe(123);
        controller.Status.ShouldBe(CashDispenseStatus.Error);
    }

    /// <summary>オブジェクトの破棄（Dispose）が例外なく実行できることを検証します。.</summary>
    [Fact]
    public void DisposeShouldNotThrow()
    {
        var controller = new DispenseController(mockManager.Object, hw, mockSimulator.Object);
        controller.Dispose();

        // Second dispose should also not throw
        controller.Dispose();
    }
}
