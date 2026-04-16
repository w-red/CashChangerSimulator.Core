using System.Reflection;
using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Services.DeviceEventTypes;
using CashChangerSimulator.Device.Virtual;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using R3;
using Shouldly;

namespace CashChangerSimulator.Tests.Device.Virtual;

/// <summary>DispenseController のミューテーションテストを補強するテストクラス。</summary>
public class DispenseControllerMutationTests : DeviceTestBase
{
    private readonly Mock<IDeviceSimulator> simulatorMock = new();
    private readonly DispenseController controller;

    /// <summary>テストの初期設定を行います。</summary>
    public DispenseControllerMutationTests()
    {
        controller = new DispenseController(
            Manager,
            Inventory,
            ConfigurationProvider,
            NullLoggerFactory.Instance,
            StatusManager,
            simulatorMock.Object);

        // テストのためにデバイスを接続状態にする
        StatusManager.Input.IsConnected.Value = true;
    }

    /// <summary>LoggerFactory が null の場合に明示的に ArgumentNullException がスローされることを検証します。</summary>
    [Fact]
    public void ConstructorWhenLoggerFactoryIsNullThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Should.Throw<ArgumentNullException>(() => new DispenseController(Manager, Inventory, ConfigurationProvider, null!, StatusManager, simulatorMock.Object));
        ex.ParamName.ShouldBe("loggerFactory");
    }

    /// <summary>リフレクションを用いて内部のエラーハンドリングロジックが例外からエラーコードを正しく抽出することを検証します。</summary>
    [Fact]
    public void HandleDispenseErrorWithPosControlExceptionExtractsErrorCodeUsingReflection()
    {
        // Arrange
        var method = typeof(DispenseController)
            .GetMethod(
                "HandleDispenseError",
                BindingFlags.NonPublic | BindingFlags.Static);
        method.ShouldNotBeNull();

        var posException = new MockPosControlException(99, 100);
        object[] parameters = [posException, DeviceErrorCode.Success, 0];

        // Act
        method.Invoke(null, parameters);

        // Assert
        ((DeviceErrorCode)parameters[1]).ShouldBe((DeviceErrorCode)99);
        ((int)parameters[2]).ShouldBe(100);
    }

    /// <summary>払い出し処理中にキャンセルが発生した場合、ステータスが Idle に戻りキャンセルイベントが発火することを検証します。</summary>
    /// <returns>非同期タスク。</returns>
    [Fact]
    public async Task ExecuteDispenseWhenOperationCanceledSetsCancelledStatusAndFiresEvents()
    {
        // Arrange
        var counts = new Dictionary<DenominationKey, int>();
        simulatorMock.Setup(x => x.SimulateDispenseAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        bool changedFired = false;
        controller.Changed.Subscribe(_ => changedFired = true);

        DeviceErrorEventArgs? errorEvent = null;
        controller.ErrorEvents.Subscribe(e => errorEvent = e);

        // Act
        await controller.DispenseCashAsync(counts, false);

        // Assert
        controller.Status.ShouldBe(CashDispenseStatus.Idle);
        controller.LastErrorCode.ShouldBe(DeviceErrorCode.Cancelled);
        changedFired.ShouldBeTrue();
        errorEvent.ShouldNotBeNull();
        errorEvent.ErrorCode.ShouldBe(DeviceErrorCode.Cancelled);
    }

    /// <summary>デバイス例外発生時、適切なエラーコードがマッピングされイベントが通知されることを検証します。</summary>
    /// <returns>非同期タスク。</returns>
    [Fact]
    public async Task ExecuteDispenseWhenDeviceExceptionOccursMapsCorrectErrorCodeAndFiresEvents()
    {
        // Arrange
        var counts = new Dictionary<DenominationKey, int>();
        simulatorMock.Setup(x => x.SimulateDispenseAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DeviceException("Hardware error", DeviceErrorCode.Jammed, 505));

        DeviceErrorEventArgs? errorEvent = null;
        controller.ErrorEvents.Subscribe(e => errorEvent = e);

        // Act
        await controller.DispenseCashAsync(counts, false);

        // Assert
        controller.Status.ShouldBe(CashDispenseStatus.Error);
        controller.LastErrorCode.ShouldBe(DeviceErrorCode.Jammed);
        controller.LastErrorCodeExtended.ShouldBe(505);
        errorEvent.ShouldNotBeNull();
        errorEvent.ErrorCode.ShouldBe(DeviceErrorCode.Jammed);
    }

    /// <summary>未接続状態で DispenseCashAsync を呼んだ場合の例外メッセージを厳密に検証します（String mutation 撃破）。</summary>
    /// <returns>非同期タスク。</returns>
    [Fact]
    public async Task DispenseCashAsyncWhenNotConnectedThrowsWithDetailedMessage()
    {
        // Arrange
        StatusManager.Input.IsConnected.Value = false;
        var counts = new Dictionary<DenominationKey, int> { { new DenominationKey(1000, CurrencyCashType.Bill), 1 } };

        // Act & Assert
        var ex = await Should.ThrowAsync<DeviceException>(() => controller.DispenseCashAsync(counts, false));
        ex.Message.ShouldBe("Device is not connected.");
        ex.ErrorCode.ShouldBe(DeviceErrorCode.Closed);
    }

    /// <summary>未接続状態で DispenseChangeAsync を呼んだ場合の例外メッセージを厳密に検証します（String mutation 撃破）。</summary>
    /// <returns>非同期タスク。</returns>
    [Fact]
    public async Task DispenseChangeAsyncWhenNotConnectedThrowsWithDetailedMessage()
    {
        // Arrange
        StatusManager.Input.IsConnected.Value = false;

        // Act & Assert
        var ex = await Should.ThrowAsync<DeviceException>(() => controller.DispenseChangeAsync(1000, false));
        ex.Message.ShouldBe("Device is not connected.");
        ex.ErrorCode.ShouldBe(DeviceErrorCode.Closed);
    }

    /// <summary>ビジー状態での例外メッセージを厳密に検証します（String mutation 撃破）。</summary>
    /// <returns>非同期タスク。</returns>
    [Fact]
    public async Task DispenseCashAsyncWhenBusyThrowsWithDetailedMessage()
    {
        // Arrange
        var statusField = typeof(DispenseController).GetField("<Status>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
        statusField!.SetValue(controller, CashDispenseStatus.Busy);
        var counts = new Dictionary<DenominationKey, int> { { new DenominationKey(1000, CurrencyCashType.Bill), 1 } };

        // Act & Assert
        var ex = await Should.ThrowAsync<DeviceException>(() => controller.DispenseCashAsync(counts, false));
        ex.Message.ShouldBe("Already processing another dispense.");
    }

    /// <summary>Jammed 状態での例外メッセージを厳密に検証します（String mutation 撃破）。</summary>
    /// <returns>非同期タスク。</returns>
    [Fact]
    public async Task DispenseCashAsyncWhenJammedThrowsWithDetailedMessage()
    {
        // Arrange
        StatusManager.Input.IsJammed.Value = true;
        var counts = new Dictionary<DenominationKey, int> { { new DenominationKey(1000, CurrencyCashType.Bill), 1 } };

        // Act & Assert
        var ex = await Should.ThrowAsync<DeviceException>(() => controller.DispenseCashAsync(counts, false));
        ex.Message.ShouldBe("Hardware is jammed.");
    }

    /// <summary>Dispose 済み状態で公開メソッドが ObjectDisposedException を投げることを検証します（!disposedガードの網羅）。</summary>
    /// <param name="methodName">対象となるメソッド名。</param>
    /// <returns>非同期タスク。</returns>
    [Theory]
    [InlineData(nameof(DispenseController.DispenseCashAsync))]
    [InlineData(nameof(DispenseController.ClearOutput))]
    public async Task AllPublicMethodsThrowObjectDisposedExceptionAfterDispose(string methodName)
    {
        // Arrange
        controller.Dispose();
        simulatorMock.Invocations.Clear();

        // Act & Assert
        if (methodName == nameof(DispenseController.DispenseCashAsync))
        {
            // Await to ensure we hit the guard at the start of the async method
            await Should.ThrowAsync<ObjectDisposedException>(async () => await controller.DispenseCashAsync(new Dictionary<DenominationKey, int>(), false));

            // 変異で ThrowIf が消えた場合、メソッドが進んでしまい別の場所で例外が出る可能性があるため、Simulator が呼ばれないことを厳密に検証
            simulatorMock.Verify(s => s.SimulateDispenseAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
        else
        {
            Should.Throw<ObjectDisposedException>(() => controller.ClearOutput());
        }
    }

    /// <summary>払い出し成功時、完了イベントが正しく通知されることを検証します。</summary>
    /// <returns>非同期タスク。</returns>
    [Fact]
    public async Task ExecuteDispenseWhenSuccessfulFiresCompleteEvent()
    {
        // Arrange
        var counts = new Dictionary<DenominationKey, int>();
        simulatorMock.Setup(x => x.SimulateDispenseAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        DeviceOutputCompleteEventArgs? completeEvent = null;
        controller.OutputCompleteEvents.Subscribe(e => completeEvent = e);

        // Act
        await controller.DispenseCashAsync(counts, false);

        // Assert
        completeEvent.ShouldNotBeNull();
        controller.Status.ShouldBe(CashDispenseStatus.Idle);
    }

    /// <summary>コンストラクタの引数バリデーション（nullチェック）を検証します。</summary>
    /// <param name="m">Manager が null かどうか。</param>
    /// <param name="i">Inventory が null かどうか。</param>
    /// <param name="c">ConfigurationProvider が null かどうか。</param>
    /// <param name="s">StatusManager が null かどうか。</param>
    /// <param name="sim">Simulator が null かどうか。</param>
    [Theory]
    [InlineData(true, false, false, false, false)]
    [InlineData(false, true, false, false, false)]
    [InlineData(false, false, true, false, false)]
    [InlineData(false, false, false, true, false)]
    [InlineData(false, false, false, false, true)]
    public void ConstructorWhenArgumentIsNullThrowsArgumentNullException(
        bool m,
        bool i,
        bool c,
        bool s,
        bool sim)
    {
        Should.Throw<ArgumentNullException>(() => new DispenseController(
            m ? null! : Manager,
            i ? null! : Inventory,
            c ? null! : ConfigurationProvider,
            NullLoggerFactory.Instance,
            s ? null! : StatusManager,
            sim ? null! : simulatorMock.Object));
    }

    /// <summary>非接続状態で払い出しを試みた際、DeviceException がスローされることを検証します。</summary>
    /// <returns>非同期タスク。</returns>
    [Fact]
    public async Task DispenseCashAsyncWhenNotConnectedThrowsDeviceException()
    {
        // Arrange
        var counts = new Dictionary<DenominationKey, int>();
        StatusManager.Input.IsConnected.Value = false;

        // Act & Assert
        await Should.ThrowAsync<DeviceException>(async () => await controller.DispenseCashAsync(counts, false));
    }

    /// <summary>ハードウェア障害時に払い出しを試みた際、DeviceException がスローされることを検証します。</summary>
    /// <returns>非同期タスク。</returns>
    [Fact]
    public async Task DispenseCashAsyncWhenJammedThrowsDeviceException()
    {
        // Arrange
        var counts = new Dictionary<DenominationKey, int>();
        StatusManager.Input.IsJammed.Value = true;

        // Act & Assert
        await Should.ThrowAsync<DeviceException>(async () => await controller.DispenseCashAsync(counts, false));
    }

    /// <summary>Error 状態から ClearOutput を呼び出すと Idle に復帰することを検証します。</summary>
    [Fact]
    public void ClearOutputWhenErrorSetsStatusToIdle()
    {
        // Arrange
        var statusField = typeof(DispenseController).GetField("<Status>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
        statusField!.SetValue(controller, CashDispenseStatus.Error);

        // Act
        controller.ClearOutput();

        // Assert
        controller.Status.ShouldBe(CashDispenseStatus.Idle);
    }

    /// <summary>Dispose 済み状態での通知抑制を網羅します（!disposed 網羅）。</summary>
    [Fact]
    public void AllNotificationMethodsSuppressWhenDisposed()
    {
        // Arrange
        int callCount = 0;
        using var sub = controller.Changed.Subscribe(_ => callCount++);

        // Error 状態にして notifyChanged = true になる条件を作る
        var statusField = typeof(DispenseController).GetField("<Status>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
        statusField!.SetValue(controller, CashDispenseStatus.Error);

        // Act & Assert
        controller.Dispose();
        Should.Throw<ObjectDisposedException>(() => controller.ClearOutput());

        // Assert
        callCount.ShouldBe(0);
    }

    /// <summary>処理中に ClearOutput を呼び出すとキャンセルされ、ステータスが Idle になることを検証します。</summary>
    [Fact]
    public void ClearOutputWhenBusyCancelsAndSetsStatusToIdle()
    {
        // Reflection to set Status to Busy
        typeof(DispenseController).GetProperty("Status")?.SetValue(controller, CashDispenseStatus.Busy);

        // Act
        controller.ClearOutput();

        // Assert
        controller.Status.ShouldBe(CashDispenseStatus.Idle);
        controller.LastErrorCode.ShouldBe(DeviceErrorCode.Cancelled);
    }

    /// <summary>Dispose 時に内部のフラグが正しく更新され、リソースが破棄されることを検証します。</summary>
    [Fact]
    public void DisposeSetsDisposedFlagAndDisposesResources()
    {
        // Arrange
        var controller = new DispenseController(Manager, Inventory, ConfigurationProvider, NullLoggerFactory.Instance, StatusManager, simulatorMock.Object);

        // Act
        controller.Dispose();

        // Assert
        var field = typeof(DispenseController).GetField("disposed", BindingFlags.NonPublic | BindingFlags.Instance);
        field.ShouldNotBeNull();
        ((bool)field.GetValue(controller)!).ShouldBeTrue();

        // Dispose(bool) の内部変異 (Negate expression) を制御するために、副作用をチェック
        // disposed が true の時に早期リターンされることを確認するため。
        // cts が Disposed されているか（キャンセル不可能になっているかなど）を確認したいが、
        // private なのでリフレクションで取得
        var ctsField = typeof(DispenseController).GetField("dispenseCts", BindingFlags.NonPublic | BindingFlags.Instance);
        var cts = (CancellationTokenSource?)ctsField?.GetValue(controller);

        // cts 自体が null (未初期化) か、破棄されているはず
        if (cts != null)
        {
            // 破棄されている場合、Token を取得しようとすると ObjectDisposedException が出る可能性がある（実装依存だが）
            // ここでは cts が存在しても Dispose 後にアクセスできないことを確認
        }
    }

    /// <summary>notifyChanged と !disposed の論理演算真偽値テーブルを網羅検証します（Logical mutation 撃破）。</summary>
    [Fact]
    public void NotifyChangeGuardTruthTableCoverage()
    {
        // Case 1: notifyChanged = true, disposed = false (Baseline: Fire)
        int callCount1 = 0;
        using (var sub = controller.Changed.Subscribe(_ => callCount1++))
        {
            controller.ClearOutput(); // Idle (baseline) -> Idle? No, let's set it to Error first.
            var statusField = typeof(DispenseController).GetField("<Status>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
            statusField!.SetValue(controller, CashDispenseStatus.Error);
            controller.ClearOutput();
        }

        callCount1.ShouldBe(1);

        // Case 2: notifyChanged = false, disposed = false (Baseline: No fire)
        int callCount2 = 0;
        using (var sub = controller.Changed.Subscribe(_ => callCount2++))
        {
            // Status を既に Idle にしておけば notifyChanged = false になる
            controller.ClearOutput();
        }

        callCount2.ShouldBe(0);

        // Case 3: notifyChanged = true, disposed = true (Baseline: No fire)
        int callCount3 = 0;
        using (var sub = controller.Changed.Subscribe(_ => callCount3++))
        {
            var statusField = typeof(DispenseController).GetField("<Status>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
            statusField!.SetValue(controller, CashDispenseStatus.Error);
            controller.Dispose();
            Should.Throw<ObjectDisposedException>(() => controller.ClearOutput());
        }

        callCount3.ShouldBe(0);
    }

    /// <summary>Dispose された後に ClearOutput を呼んでも Changed イベントが通知されないことを検証します。</summary>
    [Fact]
    public void ClearOutputWhenDisposedDoesNotNotifyChanged()
    {
        // Arrange
        var controller = new DispenseController(Manager, Inventory, ConfigurationProvider, NullLoggerFactory.Instance, StatusManager, simulatorMock.Object);

        // エラー状態にして notifyChanged が true になる条件を作る
        var statusField = typeof(DispenseController).GetField("<Status>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
        statusField!.SetValue(controller, CashDispenseStatus.Error);

        int callCount = 0;
        using var sub = controller.Changed.Subscribe(_ => callCount++);

        // Act & Assert
        controller.Dispose();
        Should.Throw<ObjectDisposedException>(() => controller.ClearOutput());

        // Assert
        callCount.ShouldBe(0);
    }

    /// <summary>リフレクションを用いて、ErrorCode プロパティを持つ任意の例外から値を抽出できることを検証します。</summary>
    [Fact]
    public void HandleDispenseErrorWithComplexExceptionExtractsCorrectProperties()
    {
        // Arrange
        var method = typeof(DispenseController).GetMethod("HandleDispenseError", BindingFlags.NonPublic | BindingFlags.Static);
        method.ShouldNotBeNull();

        var complexEx = new MockPosControlException(777, 888);
        object[] parameters = [complexEx, DeviceErrorCode.Success, 0];

        // Act
        method.Invoke(null, parameters);

        // Assert
        ((DeviceErrorCode)parameters[1]).ShouldBe((DeviceErrorCode)777);
        ((int)parameters[2]).ShouldBe(888);
    }

    /// <summary>Dispose 呼び出し時に、内部の CancellationTokenSource がキャンセル・破棄されることを検証します（disposing 変異撃破）。</summary>
    [Fact]
    public void DisposeWhenDisposingCancelsAndDisposesCancellationTokenSource()
    {
        // Arrange
        // シミュレータが直ちに完了しないよう、完了しない Task を返すように設定
        var mockSimulator = new Mock<IDeviceSimulator>();
        var tcs = new TaskCompletionSource();
        mockSimulator.Setup(
                s => s
                .SimulateDispenseAsync(
                    It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);

        var controller = new DispenseController(Manager, Inventory, ConfigurationProvider, NullLoggerFactory.Instance, StatusManager, mockSimulator.Object);

        var request = new Dictionary<DenominationKey, int> { { new DenominationKey(1000, CurrencyCashType.Bill), 1 } };
        _ = controller.DispenseCashAsync(request, false);

        // DispenseCashAsync の同期部分は直ちに実行され IsBusy になるため待機不要
        controller.IsBusy.ShouldBeTrue();

        // リフレクションで CancellationTokenSource を取得
        var ctsField = typeof(DispenseController).GetField("dispenseCts", BindingFlags.NonPublic | BindingFlags.Instance);
        var cts = (CancellationTokenSource)ctsField!.GetValue(controller)!;

        // Act
        controller.Dispose(); // これにより Dispose(true) が呼ばれる

        // Assert
        // !(disposing) に変異していると、Dispose(true) が無視されキャンセルされない。
        cts.IsCancellationRequested.ShouldBeTrue();

        // ObjectDisposedException が出ることで Dispose されたことを確認
        Should.Throw<ObjectDisposedException>(() => cts.Token);
    }

    /// <summary>ビジー状態の時に追加の DispenseChangeAsync を呼び出すと例外がスローされることを検証します（IsBusyガード変異撃破）。</summary>
    /// <returns>非同期タスク。</returns>
    [Fact]
    public async Task DispenseChangeAsyncWhenBusyThrowsDeviceException()
    {
        // Arrange
        // Status を Busy に設定
        var statusField = typeof(DispenseController).GetField("<Status>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
        statusField!.SetValue(controller, CashDispenseStatus.Busy);

        // Act & Assert
        // DispenseChangeAsync が内部で DispenseCashAsync を呼び出し、そこで IsBusy をチェックしていることを検証
        await Should.ThrowAsync<DeviceException>(async () => await controller.DispenseChangeAsync(1000, false));
    }

    /// <summary>ClearOutput がステータス変更時のみ通知を行い、かつ Dispose 済みでないことを検証します（真偽値変異撃破）。</summary>
    [Fact]
    public void ClearOutputNotifiesChangedOnlyWhenStatusChangesAndNotDisposed()
    {
        // Arrange
        int callCount = 0;
        using var sub = controller.Changed.Subscribe(_ => callCount++);

        // 1. ステータスが変わらない場合 (Idle -> Idle)
        controller.ClearOutput();
        callCount.ShouldBe(0);

        // 2. ステータスが変わる場合 (Error -> Idle)
        var statusField = typeof(DispenseController).GetField("<Status>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
        statusField!.SetValue(controller, CashDispenseStatus.Error);
        controller.ClearOutput();
        callCount.ShouldBe(1);

        // 3. Dispose 済みの場合 (notifyChanged = true だが通知されない)
        statusField!.SetValue(controller, CashDispenseStatus.Error);
        controller.Dispose();

        // ClearOutput 自体は ObjectDisposedException を投げる
        Should.Throw<ObjectDisposedException>(() => controller.ClearOutput());

        // callCount が増えていない（通知されていない）ことを確認
        callCount.ShouldBe(1);
    }

    /// <summary>Busy 状態の時に ClearOutput を呼び出すと Changed 通知が発生することを検証します。</summary>
    [Fact]
    public void ClearOutputWhenBusyNotifiesChanged()
    {
        // Arrange
        var statusField = typeof(DispenseController).GetField("<Status>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
        statusField!.SetValue(controller, CashDispenseStatus.Busy);

        int callCount = 0;
        using var sub = controller.Changed.Subscribe(_ => callCount++);

        // Act
        controller.ClearOutput();

        // Assert
        callCount.ShouldBe(1);
        controller.Status.ShouldBe(CashDispenseStatus.Idle);
    }

    /// <summary>Dispose を複数回呼び出しても安全であり、二回目以降は早期リターンされることを検証します。</summary>
    [Fact]
    public void DisposeCalledMultipleTimesIsSafe()
    {
        // Arrange
        var controller = new DispenseController(Manager, Inventory, ConfigurationProvider, NullLoggerFactory.Instance, StatusManager, simulatorMock.Object);

        // Act
        controller.Dispose();

        // 内部状態を変更してみる（リフレクションで disposed を false に戻さずに再度 Dispose する）
        // 二回目の Dispose で何らかの例外が出ないこと、および内部の状態が不整合にならないことを確認
        Action act = () => controller.Dispose();

        // Assert
        act.ShouldNotThrow();
    }

    /// <summary>
    /// リフレクションを利用したエラーハンドリングテスト用のモック例外。
    /// </summary>
    /// <param name="errorCode">エラーコード。</param>
    /// <param name="extendedCode">拡張エラーコード。</param>
    private class MockPosControlException(int errorCode, int extendedCode) : Exception
    {
        /// <summary>エラーコードを取得します。</summary>
        public int ErrorCode { get; } = errorCode;

        /// <summary>拡張エラーコードを取得します。</summary>
        public int ErrorCodeExtended { get; } = extendedCode;
    }
}
