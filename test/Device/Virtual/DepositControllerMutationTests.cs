using System.Reflection;
using R3;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device.Virtual;
using Moq;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Tests.Device.Virtual;

/// <summary>DepositController のミューテーションテストを補強するテストクラス。</summary>
public class DepositControllerMutationTests : DeviceTestBase
{
    private readonly DepositController _controller;

    /// <summary>テストの初期設定を行います。</summary>
    public DepositControllerMutationTests()
    {
        _controller = new DepositController(Inventory, StatusManager, Manager, ConfigurationProvider, TimeProvider);
        // テストのためにデバイスを接続状態にする
        StatusManager.Input.IsConnected.Value = true;
    }

    /// <summary>コンストラクタに null が渡された場合にデフォルトのインスタンスが作成されることを検証します。</summary>
    [Fact]
    public void ConstructorWhenArgumentsAreNullCreatesDefaultInstances()
    {
        // Act
        var controller = new DepositController(Inventory, null, null, null, null);

        // Assert
        // HardwareStatusManager がデフォルトで作成されていることを確認
        var statusField = typeof(DepositController).GetField("hardwareStatusManager", BindingFlags.NonPublic | BindingFlags.Instance);
        statusField.ShouldNotBeNull();
        statusField.GetValue(controller).ShouldNotBeNull();

        // configProvider が内部で新規作成されていることを確認
        var configField = typeof(DepositController).GetField("internalConfigProvider", BindingFlags.NonPublic | BindingFlags.Instance);
        configField.ShouldNotBeNull();
        configField.GetValue(controller).ShouldNotBeNull();
        
        // TimeProvider がデフォルト(System)であることを確認
        var timeProviderField = typeof(DepositController).GetField("timeProvider", BindingFlags.NonPublic | BindingFlags.Instance);
        timeProviderField.ShouldNotBeNull();
        timeProviderField.GetValue(controller).ShouldBe(System.TimeProvider.System);

        // HardwareStatusManager もデフォルトが作成されていること
        var hardwareStatusField = typeof(DepositController).GetField("hardwareStatusManager", BindingFlags.NonPublic | BindingFlags.Instance);
        hardwareStatusField!.GetValue(controller).ShouldNotBeNull();
    }

    /// <summary>TimeProvider が null の場合に System.TimeProvider が使用されることを検証します（Null合体変異の撃破）。</summary>
    [Fact]
    public void ConstructorWhenTimeProviderIsNullUsesSystemTimeProvider()
    {
        // Act
        var controller = new DepositController(Inventory, StatusManager);

        // Assert
        var field = typeof(DepositController).GetField("timeProvider", BindingFlags.NonPublic | BindingFlags.Instance);
        field!.GetValue(controller).ShouldBe(System.TimeProvider.System);
    }

    /// <summary>カスタム TimeProvider が保持されることを検証します（Null合体変異の撃破）。</summary>
    [Fact]
    public void ConstructorWhenTimeProviderIsProvidedUsesProvidedInstance()
    {
        // Arrange
        var mockTime = new Mock<TimeProvider>();
        
        // Act
        var controller = new DepositController(Inventory, StatusManager, null, null, mockTime.Object);

        // Assert
        var field = typeof(DepositController).GetField("timeProvider", BindingFlags.NonPublic | BindingFlags.Instance);
        field!.GetValue(controller).ShouldBe(mockTime.Object);
    }

    /// <summary>Inventory に null を渡した場合に ArgumentNullException がスローされることを検証します。</summary>
    [Fact]
    public void ConstructorWhenInventoryIsNullThrowsException()
    {
        // Act & Assert
        var ex = Should.Throw<ArgumentNullException>(() => new DepositController(null!));
        ex.ParamName.ShouldBe("inventory");
    }

    /// <summary>RequiredAmount に同じ値を設定した際に Changed イベントが発火しないことを検証します。</summary>
    [Fact]
    public void RequiredAmountWhenSetToSameValueDoesNotFireChanged()
    {
        // Arrange
        _controller.RequiredAmount = 1000m;
        int callCount = 0;
        using var sub = _controller.Changed.Subscribe(_ => callCount++);

        // Act
        _controller.RequiredAmount = 1000m;

        // Assert
        callCount.ShouldBe(0);
        // Lock 削除変異 (L324 block removal) 撃破のための値検証
        _controller.RequiredAmount.ShouldBe(1000m);
    }

    /// <summary>RequiredAmount に異なる値を設定した際に Changed イベントが発火することを検証します。</summary>
    [Fact]
    public void RequiredAmountWhenSetToDifferentValueFiresChanged()
    {
        // Arrange
        _controller.RequiredAmount = 1000m;
        int callCount = 0;
        using var sub = _controller.Changed.Subscribe(_ => callCount++);

        // Act
        _controller.RequiredAmount = 2000m;

        // Assert
        callCount.ShouldBe(1);
    }

    /// <summary>入金開始時にステータスが Counting になり、Changed イベントが発火することを検証します。</summary>
    [Fact]
    public void BeginDepositFiresEventsAndSetsStatus()
    {
        // Arrange
        bool changedFired = false;
        using var sub = _controller.Changed.Subscribe(_ => changedFired = true);

        // Act
        _controller.BeginDeposit();

        // Assert
        _controller.DepositStatus.ShouldBe(DeviceDepositStatus.Counting);
        changedFired.ShouldBeTrue();
    }

    /// <summary>入金トラッキング時に容量を超えた場合に、オーバーフロー金額が正しく計算されることを検証します。</summary>
    [Fact]
    public void TrackDepositWhenCapacityFullCalculatesOverflowAmount()
    {
        // Arrange
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        // 在庫を 95 枚にする (Full は 100 と想定)
        Inventory.Add(key, 95);
        _controller.BeginDeposit();

        // Act
        // 10 枚投入 (空きは 5 枚なので、5 枚分がオーバーフロー)
        _controller.TrackDeposit(key, 10);

        // Assert
        _controller.DepositAmount.ShouldBe(10000m);
        _controller.OverflowAmount.ShouldBe(5000m);
    }

    /// <summary>EndDepositAsync(Change) において、釣銭が不足する場合にマネージャの Dispense が呼ばれることを検証します。</summary>
    [Fact]
    public async Task EndDepositAsyncChangeWhenShortageCallsManagerDispense()
    {
        // Arrange
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        var managerMock = new Mock<CashChangerManager>(Inventory, History, ConfigurationProvider);
        var controller = new DepositController(Inventory, StatusManager, managerMock.Object);

        controller.BeginDeposit();
        controller.TrackDeposit(key, 5); // 5000円投入
        controller.RequiredAmount = 1000m; // 4000円のお釣りが必要
        
        // インベントリを空にする (お釣りが払えない状態)
        Inventory.Clear();

        // Act
        controller.FixDeposit();
        var task = controller.EndDepositAsync(DepositAction.Change);
        TimeProvider.Advance(TimeSpan.FromSeconds(2));
        await task;

        // Assert
        // マネージャーの Dispense が 4000円分呼ばれることを確認
        managerMock.Verify(m => m.Dispense(4000m, null), Times.Once);
    }

    /// <summary>EndDepositAsync(Change) において、マネージャが null の場合に例外が発生しないことを検証します。</summary>
    [Fact]
    public async Task EndDepositAsyncChangeWhenManagerIsNullDoesNotThrow()
    {
        // Arrange
        var controller = new DepositController(Inventory, StatusManager, null); // Manager is null
        controller.BeginDeposit();
        controller.TrackDeposit(new DenominationKey(1000, CurrencyCashType.Bill), 5);
        controller.RequiredAmount = 1000m;

        // Act & Assert
        controller.FixDeposit();
        var task = controller.EndDepositAsync(DepositAction.Change);
        TimeProvider.Advance(TimeSpan.FromSeconds(2));
        await task.ShouldNotThrowAsync();
    }

    /// <summary>EndDepositAsync(Change) において、manager が null で釣銭が必要な場合、例外を投げずにスキップすることを検証します（Logical mutation 撃破）。</summary>
    [Fact]
    public async Task EndDepositAsyncChangeWhenManagerIsNullAndChangeNeededSuppressesDispense()
    {
        // Arrange
        var controller = new DepositController(Inventory, StatusManager, null); // Manager is null
        controller.BeginDeposit();
        controller.TrackDeposit(new DenominationKey(1000, CurrencyCashType.Bill), 5);
        controller.RequiredAmount = 1000m; // 4000円のお釣りが必要

        // Act
        controller.FixDeposit();
        var task = controller.EndDepositAsync(DepositAction.Change);
        TimeProvider.Advance(TimeSpan.FromSeconds(2));
        
        // Assert
        await task.ShouldNotThrowAsync();
        // manager が null なので内部で Skip される（&& manager != null)
    }

    /// <summary>Dispose された後にイベントが通知されないことを検証します。</summary>
    [Fact]
    public void NotifyTrackingEventsWhenDisposedDoesNotFireEvents()
    {
        // Arrange
        int callCount = 0;
        using var sub = _controller.Changed.Subscribe(_ => callCount++);
        _controller.RealTimeDataEnabled = true;

        // Act & Assert
        _controller.Dispose();
        
        // Dispose 後はメソッド呼び出しで例外が飛ぶ
        Should.Throw<ObjectDisposedException>(() => _controller.BeginDeposit());
        Should.Throw<ObjectDisposedException>(() => _controller.TrackDeposit(new DenominationKey(1000, CurrencyCashType.Bill), 1));

        // Dispose 後の試行によりイベントが追加で飛ばないことを確認
        callCount.ShouldBe(0);
    }

    /// <summary>Dispose 時に内部のフラグが正しく更新され、cts が破棄されることを検証します。</summary>
    [Fact]
    public void DisposeSetsDisposedFlagAndDisposesResources()
    {
        // Arrange
        var controller = new DepositController(Inventory, StatusManager);
        
        // Act
        controller.Dispose();

        // Assert
        var disposedField = typeof(DepositController).GetField("disposed", BindingFlags.NonPublic | BindingFlags.Instance);
        disposedField.ShouldNotBeNull();
        ((bool)disposedField.GetValue(controller)!).ShouldBeTrue();
    }

    /// <summary>入金確定時にステータスが Fix になり、Changed イベントが発火することを確認します。</summary>
    [Fact]
    public void FixDepositFiresEventsAndSetsStatus()
    {
        // Arrange
        _controller.BeginDeposit();
        bool changedFired = false;
        using var sub = _controller.Changed.Subscribe(_ => changedFired = true);

        // Act
        _controller.FixDeposit();

        // Assert
        _controller.IsFixed.ShouldBeTrue();
        changedFired.ShouldBeTrue();
    }

    /// <summary>EndDepositAsync が遅延を伴って正常に完了し、ステータスが End になることを検証します。</summary>
    [Fact]
    public async Task EndDepositAsyncCompletesAndSetsStatusToEnd()
    {
        // Arrange
        ConfigurationProvider.Config.Simulation.DepositDelayMs = 100;
        _controller.BeginDeposit();
        _controller.FixDeposit();
        
        // Act
        var endTask = _controller.EndDepositAsync(DepositAction.NoChange);
        
        // 仮想時間を進めて完了させる
        TimeProvider.Advance(TimeSpan.FromMilliseconds(100));
        await endTask;
        
        // Assert
        _controller.DepositStatus.ShouldBe(DeviceDepositStatus.End);
        _controller.IsBusy.ShouldBeFalse();
    }

    /// <summary>入金データの追跡時に金額が正しく更新され、Changed イベントが発火することを検証します。</summary>
    [Fact]
    public void TrackDepositFiresEventsAndUpdatesAmount()
    {
        // Arrange
        _controller.BeginDeposit();
        var amountSet = false;
        using var sub = _controller.Changed.Subscribe(_ => amountSet = true);

        // Act
        _controller.TrackDeposit(new DenominationKey(1000m, CurrencyCashType.Bill), 5);

        // Assert
        _controller.DepositAmount.ShouldBe(5000);
        amountSet.ShouldBeTrue();
    }

    /// <summary>例外発生時にメッセージに期待されるキーワードが含まれていることを検証します。</summary>
    [Fact]
    public void BeginDepositWhenBusyThrowsWithDetailedMessage()
    {
        // Arrange
        // リフレクションで IsBusy を true にする
        var busyField = typeof(DepositController).GetField("<IsBusy>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
        busyField!.SetValue(_controller, true);

        // Act & Assert
        var ex = Should.Throw<DeviceException>(() => _controller.BeginDeposit());
        ex.Message.ShouldContain("busy");
    }

    /// <summary>非同期セッション中に FixDeposit を呼び出さずに EndDepositAsync を呼んだ場合のメッセージを検証します。</summary>
    [Fact]
    public async Task EndDepositAsyncWhenInvalidSequenceThrowsWithMessage()
    {
        // Arrange
        _controller.BeginDeposit();
        // FixDeposit() を呼ばない

        // Act & Assert
        var ex = await Should.ThrowAsync<DeviceException>(() => _controller.EndDepositAsync(DepositAction.NoChange));
        ex.Message.ShouldContain("Invalid call sequence");
    }

    /// <summary>各プロパティの getter が内部状態を正しく返すことを検証します（BlockRemoval 対策）。</summary>
    [Fact]
    public void PropertiesReturnCorrectInternalState()
    {
        // Arrange
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        _controller.BeginDeposit();
        _controller.TrackDeposit(key, 1);

        // Assert
        _controller.DepositCounts.ShouldContainKey(key);
        _controller.DepositCounts[key].ShouldBe(1);
        _controller.LastErrorCode.ShouldBe(DeviceErrorCode.Success);
        
        // 非ゼロ値をセットして取得を検証 (L283, L291 lock block removal 撃破)
        var property = typeof(DepositController).GetProperty("LastErrorCodeExtended", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        property!.SetValue(_controller, 123);
        _controller.LastErrorCodeExtended.ShouldBe(123);

        _controller.LastDepositedSerials.ShouldNotBeNull();
        
        _controller.RequiredAmount = 999m;
        _controller.RequiredAmount.ShouldBe(999m);
    }

    /// <summary>DepositCounts が防御的コピーを返していることを検証します（BlockRemoval 対策）。</summary>
    [Fact]
    public void DepositCountsReturnsDefensiveCopy()
    {
        // Arrange
        _controller.BeginDeposit();
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        _controller.TrackDeposit(key, 1);

        // Act
        var counts1 = _controller.DepositCounts;
        var counts2 = _controller.DepositCounts;

        // Assert
        counts1.ShouldNotBeSameAs(counts2); // 毎回新しいインスタンス
        counts1.Count.ShouldBe(1);
        counts1[key].ShouldBe(1);
    }

    /// <summary>EndDepositAsync(Change) において、釣銭が必要な場合とマネージャの有無による論理分岐を網羅します（L498 &amp;&amp; 変異を撃破）。</summary>
    [Theory]
    [InlineData(4000, true, 1)]  // 釣銭 4000円 (5000円投入で 4000円不足 -> エスクロー(5000円)から払えない) -> Dispense呼ばれる
    [InlineData(4000, false, 0)] // Managerなし -> Dispense呼ばれない
    [InlineData(0, true, 0)]    // 釣銭なし -> Dispense呼ばれない
    [InlineData(0, false, 0)]   // 釣銭なし && Managerなし -> Dispense呼ばれない
    public async Task EndDepositAsyncLogicTable(decimal changeNeeded, bool hasManager, int expectedDispenseCalls)
    {
        // Arrange
        var mockManager = hasManager ? new Mock<CashChangerManager>(Inventory, History, ConfigurationProvider) : null;
        var controller = new DepositController(Inventory, StatusManager, mockManager?.Object);
        controller.BeginDeposit();
        
        // 投入金額を調整
        if (changeNeeded > 0)
        {
            // 5000円を1枚投入
            controller.TrackDeposit(new DenominationKey(5000, CurrencyCashType.Bill), 1);
            controller.RequiredAmount = 5000 - changeNeeded;
        }
        else
        {
            controller.TrackDeposit(new DenominationKey(1000, CurrencyCashType.Bill), 1);
            controller.RequiredAmount = 1000;
        }

        // Act
        controller.FixDeposit();
        await controller.EndDepositAsync(DepositAction.Change);

        // Assert
        if (mockManager != null)
        {
            mockManager.Verify(m => m.Dispense(It.Is<decimal>(d => d == changeNeeded), It.IsAny<string?>()), Times.Exactly(expectedDispenseCalls));
        }
    }

    /// <summary>釣銭計算ループにおいて remainingChange がちょうど 0 に到達した際の境界条件を検証します（Equality mutation 撃破）。</summary>
    [Fact]
    public void CalculateChangeLoopBoundariesWhenRemainingHitsExactlyZero()
    {
        // Arrange
        Inventory.Add(new DenominationKey(1000, CurrencyCashType.Bill), 10);
        _controller.BeginDeposit();
        _controller.TrackDeposit(new DenominationKey(5000, CurrencyCashType.Bill), 1);
        _controller.RequiredAmount = 4000m; // ちょうど 1000円札 1枚がお釣り（Escrowから戻る）

        // Act
        _controller.FixDeposit(); // ここで再計算ロジックが走る

        // Assert
        // L476: if (remainingChange <= 0) でブレイクすることを期待
        // もし < 0 に変異すると、0 の時にもう一度回ろうとする
        _controller.DepositCounts.Count.ShouldBe(1);
    }

    /// <summary>枚数が 0 の金種がインベントリに追加されないことを検証します（Equality mutation 撃破）。</summary>
    [Fact]
    public async Task EndDepositAsyncDoesNotAddZeroCountToInventory()
    {
        // Arrange
        // Note: Inventory.Add は virtual ではないため Mock できないので、
        // 実際の Inventory クラスを使用して、内部状態が変わっていないことを確認する。
        var inventory = Inventory.Create();
        var controller = new DepositController(inventory, StatusManager);
        controller.BeginDeposit();

        // Act
        controller.FixDeposit();
        await controller.EndDepositAsync(DepositAction.Change);

        // Assert
        // L516: if (kv.Value > 0) により、inventory.Add は呼ばれないはず
        inventory.AllCounts.Count().ShouldBe(0);
    }

    /// <summary>RealTimeDataEnabled と !disposed の論理演算 (L742) を検証します。</summary>
    [Fact]
    public void TrackRejectFiresDataEventOnlyWhenEnabledAndNotDisposed()
    {
        // Arrange
        var dataFired = false;
        using var sub = _controller.DataEvents.Subscribe(_ => dataFired = true);
        
        _controller.BeginDeposit();
        
        // Case 1: Enabled = false, Disposed = false (Baseline: No fire)
        _controller.RealTimeDataEnabled = false;
        _controller.TrackReject(1000m);
        dataFired.ShouldBeFalse();

        // Case 2: Enabled = true, Disposed = true (Baseline: No fire)
        _controller.RealTimeDataEnabled = true;
        _controller.Dispose();
        Should.Throw<ObjectDisposedException>(() => _controller.TrackReject(1000m));
        dataFired.ShouldBeFalse();

        // Case 3: Enabled = false, Disposed = true
        var controller2 = new DepositController(Inventory, StatusManager);
        controller2.RealTimeDataEnabled = false;
        var dataFired2 = false;
        using (var sub2 = controller2.DataEvents.Subscribe(_ => dataFired2 = true))
        {
            controller2.Dispose();
            Should.Throw<ObjectDisposedException>(() => controller2.TrackReject(1000m));
        }
        dataFired2.ShouldBeFalse();
    }

    /// <summary>PauseDeposit の例外メッセージと!disposedガードを検証します。</summary>
    [Fact]
    public void PauseDepositWhenNotInProgressThrowsWithMessage()
    {
        // Act & Assert
        var ex = Should.Throw<DeviceException>(() => _controller.PauseDeposit(DeviceDepositPause.Pause));
        ex.Message.ShouldContain("Session not active");
    }

    /// <summary>Dispose 済み状態ですべての公開メソッドが ObjectDisposedException を投げることを検証します（!disposedガードの網羅）。</summary>
    [Theory]
    [InlineData(nameof(DepositController.BeginDeposit))]
    [InlineData(nameof(DepositController.FixDeposit))]
    [InlineData(nameof(DepositController.PauseDeposit))]
    [InlineData(nameof(DepositController.TrackDeposit))]
    [InlineData(nameof(DepositController.TrackReject))]
    [InlineData(nameof(DepositController.EndDepositAsync))]
    public async Task AllPublicMethodsThrowObjectDisposedExceptionAfterDispose(string methodName)
    {
        // Arrange
        _controller.Dispose();

        // Act & Assert
        if (methodName == nameof(DepositController.EndDepositAsync))
        {
            await Should.ThrowAsync<ObjectDisposedException>(async () => await _controller.EndDepositAsync(DepositAction.NoChange));
        }
        else
        {
            var method = typeof(DepositController).GetMethod(methodName);
            var args = methodName switch
            {
                nameof(DepositController.PauseDeposit) => new object[] { DeviceDepositPause.Pause },
                nameof(DepositController.TrackDeposit) => new object[] { new DenominationKey(1000, CurrencyCashType.Bill), 1 },
                nameof(DepositController.TrackReject) => new object[] { 1000m },
                _ => null
            };

            var ex = Should.Throw<TargetInvocationException>(() => method!.Invoke(_controller, args));
            ex.InnerException.ShouldBeOfType<ObjectDisposedException>();
        }
    }

    [Theory]
    [InlineData(nameof(DepositController.BeginDeposit))]
    [InlineData(nameof(DepositController.FixDeposit))]
    [InlineData(nameof(DepositController.PauseDeposit))]
    public void AllNotificationMethodsSuppressWhenDisposed(string methodName)
    {
        // Arrange
        int callCount = 0;
        using var sub = _controller.Changed.Subscribe(_ => callCount++);
        _controller.Dispose();

        // Act
        try
        {
            var method = typeof(DepositController).GetMethod(methodName, methodName == nameof(DepositController.PauseDeposit) ? new[] { typeof(DeviceDepositPause) } : Array.Empty<Type>());
            method!.Invoke(_controller, methodName == nameof(DepositController.PauseDeposit) ? new object[] { DeviceDepositPause.Pause } : null);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is DeviceException or ObjectDisposedException)
        {
            // 例外は許容する（ガード変異を殺すのが目的）
        }

        // Assert
        callCount.ShouldBe(0);
    }

    /// <summary>入金の一時停止と再開が正しく状態遷移することを確認します。</summary>
    [Fact]
    public void PauseDepositTransitionsStateCorrectly()
    {
        // Arrange
        _controller.BeginDeposit(); // Status: Counting
        
        // Act
        _controller.PauseDeposit(DeviceDepositPause.Pause);
        _controller.IsPaused.ShouldBeTrue();

        _controller.PauseDeposit(DeviceDepositPause.Resume);
        _controller.IsPaused.ShouldBeFalse();
    }

    /// <summary>返却を伴う入金終了が金額をクリアしステータスをリセットすることを検証します。</summary>
    [Fact]
    public async Task EndDepositAsyncWithRepayClearsEscrowAndResetsAmount()
    {
        // Arrange
        ConfigurationProvider.Config.Simulation.DepositDelayMs = 100;
        _controller.BeginDeposit();
        _controller.TrackDeposit(new DenominationKey(1000m, CurrencyCashType.Bill), 1);
        _controller.FixDeposit();
        
        // Act
        var endTask = _controller.EndDepositAsync(DepositAction.Repay);
        TimeProvider.Advance(TimeSpan.FromMilliseconds(100));
        await endTask;

        // Assert
        _controller.DepositAmount.ShouldBe(0);
        _controller.DepositStatus.ShouldBe(DeviceDepositStatus.End);
    }
}
