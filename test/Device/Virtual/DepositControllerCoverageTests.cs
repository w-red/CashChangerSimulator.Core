using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device.Virtual;
using Shouldly;

namespace CashChangerSimulator.Tests.Device.Virtual;

/// <summary>DepositController の受入制御ロジックを網羅的に検証するテストクラス。</summary>
public class DepositControllerCoverageTests : DeviceTestBase
{
    private readonly DepositController controller;

    public DepositControllerCoverageTests()
    {
        // Default behavior for these tests
        StatusManager.SetConnected(true);
        controller = new DepositController(Inventory, StatusManager, manager: null, configProvider: ConfigurationProvider);
    }

    /// <summary>IsBusy プロパティが初期状態で偽であることを検証する。</summary>
    [Fact]
    public void PropertyIsBusyShouldReturnExpectedValue()
    {
        controller.IsBusy.ShouldBeFalse();
    }

    /// <summary>RequiredAmount プロパティの設定と取得が正しく行われることを検証する。</summary>
    [Fact]
    public void PropertyRequiredAmountCanBeSetAndRetrieved()
    {
        controller.RequiredAmount = 1500m;
        controller.RequiredAmount.ShouldBe(1500m);

        // Coverage for same value branch
        controller.RequiredAmount = 1500m;
        controller.RequiredAmount.ShouldBe(1500m);
    }

    /// <summary>リジェクト金額の取得と加算が受入中のみ有効であることを検証する。</summary>
    [Fact]
    public void PropertyRejectAmountAndTrackRejectShouldWorkCorrectly()
    {
        controller.BeginDeposit();

        controller.RejectAmount.ShouldBe(0m);
        controller.TrackReject(500m);
        controller.RejectAmount.ShouldBe(500m);
    }

    /// <summary>受入中でない場合のリジェクト加算が無視されることを検証する。</summary>
    [Fact]
    public void TrackRejectShouldDoNothingWhenDepositNotInProgress()
    {
        controller.TrackReject(500m);
        controller.RejectAmount.ShouldBe(0m);
    }

    /// <summary>RepayDepositAsync が状態をクリアし終了イベントを発火することを検証する。</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task RepayDepositShouldClearStateAndRaiseEvent()
    {
        controller.BeginDeposit();
        controller.TrackReject(100m);

        await controller.RepayDepositAsync();

        controller.DepositAmount.ShouldBe(0m);
        controller.RejectAmount.ShouldBe(100m);
        controller.DepositStatus.ShouldBe(DeviceDepositStatus.End);
    }

    /// <summary>Dispose を複数回呼び出しても例外が発生しないことを検証する。</summary>
    [Fact]
    public void DisposeWhenCalledMultipleTimesShouldNotThrow()
    {
        controller.Dispose();
        Should.NotThrow(() => controller.Dispose());
    }

    /// <summary>一時停止と再開が受入処理に正しく反映されることを検証する。</summary>
    [Fact]
    public void PauseDepositShouldHandleEdgeCases()
    {
        controller.BeginDeposit();

        controller.PauseDeposit(DeviceDepositPause.Pause);

        controller.TrackDeposit(new DenominationKey(1000, CurrencyCashType.Bill));
        controller.DepositAmount.ShouldBe(0m);

        controller.PauseDeposit(DeviceDepositPause.Resume);

        controller.TrackDeposit(new DenominationKey(1000, CurrencyCashType.Bill));
        controller.DepositAmount.ShouldBe(1000m);
    }

    /// <summary>詰まり発生中に入金を開始しようとすると例外が発生することを検証する。</summary>
    [Fact]
    public void BeginDepositWhenJammedShouldThrow()
    {
        StatusManager.SetJammed(true);
        Should.Throw<DeviceException>(() => controller.BeginDeposit())
            .ErrorCode.ShouldBe(DeviceErrorCode.Jammed);
    }

    /// <summary>重なり発生中に入金を開始しようとすると例外が発生することを検証する。</summary>
    [Fact]
    public void BeginDepositWhenOverlappedShouldThrow()
    {
        StatusManager.SetOverlapped(true);
        Should.Throw<DeviceException>(() => controller.BeginDeposit())
            .ErrorCode.ShouldBe(DeviceErrorCode.Overlapped);
    }

    /// <summary>一時停止中にバルク入金を行っても金額が反映されないことを検証する。</summary>
    [Fact]
    public void TrackBulkDepositWhenPausedShouldIgnore()
    {
        controller.BeginDeposit();
        controller.PauseDeposit(DeviceDepositPause.Pause);

        controller.TrackBulkDeposit(new Dictionary<DenominationKey, int>
        {
            { new DenominationKey(1000, CurrencyCashType.Bill), 1 }
        });

        controller.DepositAmount.ShouldBe(0m);
    }

    /// <summary>受入確定後にバルク入金を行おうとすると例外が発生することを検証する。</summary>
    [Fact]
    public void TrackBulkDepositWhenFixedShouldThrow()
    {
        controller.BeginDeposit();
        controller.FixDeposit();

        Should.Throw<DeviceException>(() => controller.TrackBulkDeposit(new Dictionary<DenominationKey, int>
        {
            { new DenominationKey(1000, CurrencyCashType.Bill), 1 }
        }))
        .ErrorCode.ShouldBe(DeviceErrorCode.Illegal);
    }

    /// <summary>確定前に EndDeposit を呼び出そうとすると例外が発生することを検証する。</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task EndDepositWithoutFixShouldThrow()
    {
        controller.BeginDeposit();

        (await Should.ThrowAsync<DeviceException>(async () => await controller.EndDepositAsync(DepositAction.NoChange)))
            .ErrorCode.ShouldBe(DeviceErrorCode.Illegal);
    }

    /// <summary>同期版の RepayDeposit が正しく動作することを検証する。</summary>
    [Fact]
    public void RepayDepositSynchronousShouldWork()
    {
        controller.BeginDeposit();
        Should.NotThrow(() => controller.RepayDeposit());
        controller.DepositStatus.ShouldBe(DeviceDepositStatus.End);
    }

    /// <summary>入金中に EndDepositAsync を呼び出すと Busy エラーが発生することを検証する。</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task EndDepositAsyncWhenAlreadyBusyShouldThrow()
    {
        controller.BeginDeposit();
        controller.FixDeposit();

        // Start an operation that sets isBusy = true
        var task = controller.EndDepositAsync(DepositAction.NoChange);

        // Concurrent call should throw Busy
        (await Should.ThrowAsync<DeviceException>(async () => await controller.EndDepositAsync(DepositAction.NoChange)))
            .ErrorCode.ShouldBe(DeviceErrorCode.Busy);

        await task;
    }

    /// <summary>入金中に BeginDeposit を呼び出すと Busy エラーが発生することを検証する。</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task BeginDepositWhenBusyShouldThrow()
    {
        controller.BeginDeposit();
        controller.FixDeposit();

        // Start an operation that sets isBusy = true
        var task = controller.EndDepositAsync(DepositAction.NoChange);

        // While EndDepositAsync is running, isBusy is true
        Should.Throw<DeviceException>(() => controller.BeginDeposit())
            .ErrorCode.ShouldBe(DeviceErrorCode.Busy);

        await task;
    }

    /// <summary>EndDepositAsync 実行中に Overlapped が検出されて例外が発生することを検証する。</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task EndDepositAsyncShouldCatchOverlappedException()
    {
        controller.BeginDeposit();
        controller.FixDeposit();

        var task = controller.EndDepositAsync(DepositAction.NoChange);

        // Wait briefly for Task.Delay(500)
        await Task.Delay(100).ConfigureAwait(false);
        StatusManager.SetOverlapped(true);

        await task;

        controller.LastErrorCode.ShouldBe(DeviceErrorCode.Overlapped);
    }

    /// <summary>EndDepositAsync 実行中にキャンセルが発生することを検証する。</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task EndDepositAsyncShouldHandleCancellation()
    {
        controller.BeginDeposit();
        controller.FixDeposit();

        var task = controller.EndDepositAsync(DepositAction.NoChange);

        // Wait briefly to ensure Task.Delay started
        await Task.Delay(50);

        controller.Dispose(); // This cancels the internal CTS

        await task;
        controller.LastErrorCode.ShouldBe(DeviceErrorCode.Cancelled);
    }

    /// <summary>既に確定（Fixed）した状態で入金を追跡しようとし、警告が想定通りに処理されることを検証する。</summary>
    [Fact]
    public void TrackDepositWhenFixedShouldThrow()
    {
        controller.BeginDeposit();
        controller.FixDeposit();

        Should.Throw<DeviceException>(() => controller.TrackDeposit(new DenominationKey(1000, CurrencyCashType.Bill)))
            .ErrorCode.ShouldBe(DeviceErrorCode.Illegal);
    }
}
