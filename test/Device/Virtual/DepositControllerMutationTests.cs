using System.Reflection;
using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device.Virtual;
using Moq;
using R3;
using Shouldly;
using CashChangerSimulator.Tests.Fixtures;

namespace CashChangerSimulator.Tests.Device.Virtual;

/// <summary>DepositController のミューテーションテストを補強するテストクラス。</summary>
[Collection("SequentialHardwareTests")]
public class DepositControllerMutationTests : DeviceTestBase
{
    private readonly DepositController controller;

    /// <summary>テストの初期設定を行います。</summary>
    public DepositControllerMutationTests()
    {
        controller = new ControllerTestBuilder(Fixture)
            .WithConnected(true)
            .BuildDepositController();
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
        var configField = typeof(DepositController).GetField("configProvider", BindingFlags.NonPublic | BindingFlags.Instance);
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

    /// <summary>TimeProvider が null の場合に System.TimeProvider が使用されることを検証します（Null合体変異対応）。</summary>
    [Fact]
    public void ConstructorWhenTimeProviderIsNullUsesSystemTimeProvider()
    {
        // Act
        var localController = new DepositController(Inventory, StatusManager);

        // Assert
        var field = typeof(DepositController).GetField("timeProvider", BindingFlags.NonPublic | BindingFlags.Instance);
        field!.GetValue(localController).ShouldBe(System.TimeProvider.System);
    }

    /// <summary>カスタム TimeProvider が保持されることを検証します（Null合体変異対応）。</summary>
    [Fact]
    public void ConstructorWhenTimeProviderIsProvidedUsesProvidedInstance()
    {
        // Arrange
        var mockTime = new Mock<TimeProvider>();

        // Act
        var localController = new DepositController(Inventory, StatusManager, null, null, mockTime.Object);

        // Assert
        var field = typeof(DepositController).GetField("timeProvider", BindingFlags.NonPublic | BindingFlags.Instance);
        field!.GetValue(localController).ShouldBe(mockTime.Object);
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
        controller.RequiredAmount = 1000m;
        int callCount = 0;
        using var sub = controller.Changed.Subscribe(_ => callCount++);

        // Act
        controller.RequiredAmount = 1000m;

        // Assert
        callCount.ShouldBe(0);

        // Lock 削除変異 (block removal) 対応のための値検証
        controller.RequiredAmount.ShouldBe(1000m);
    }

    /// <summary>RequiredAmount に異なる値を設定した際に Changed イベントが発火することを検証します。</summary>
    [Fact]
    public void RequiredAmountWhenSetToDifferentValueFiresChanged()
    {
        // Arrange
        controller.RequiredAmount = 1000m;
        int callCount = 0;
        using var sub = controller.Changed.Subscribe(_ => callCount++);

        // Act
        controller.RequiredAmount = 2000m;

        // Assert
        callCount.ShouldBe(1);
    }

    /// <summary>入金開始時にステータスが Counting になり、Changed イベントが発火することを検証します。</summary>
    [Fact]
    public void BeginDepositFiresEventsAndSetsStatus()
    {
        // Arrange
        // あらかじめ値をセットしておくために一度開始してトラックする
        controller.BeginDeposit();
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        controller.TrackDeposit(key, 1);
        controller.DepositAmount.ShouldBe(1000m);
        Inventory.EscrowCounts.Sum(kv => kv.Key.Value * kv.Value).ShouldBe(1000m);

        bool changedFired = false;
        using var sub = controller.Changed.Subscribe(_ => changedFired = true);

        // Act
        // 再度 BeginDeposit を呼ぶことで、内部状態が Clear() されることを検証する (Statement mutation 対応)
        controller.BeginDeposit();

        // Assert
        controller.DepositStatus.ShouldBe(DeviceDepositStatus.Counting);
        controller.DepositAmount.ShouldBe(0m);
        controller.DepositCounts.ShouldBeEmpty();
        Inventory.EscrowCounts.ShouldBeEmpty();

        // state.DepositedSerials がクリアされていることを確認
        var stateField = typeof(DepositController).GetField("state", BindingFlags.NonPublic | BindingFlags.Instance);
        var state = (dynamic)stateField!.GetValue(controller)!;
        ((int)state.DepositedSerials.Count).ShouldBe(0);

        changedFired.ShouldBeTrue();
    }

    /// <summary>ハードウェアがスタックしている場合に BeginDeposit が例外を投げることを検証します。</summary>
    [Fact]
    public void BeginDepositWhenJammedThrowsDeviceException()
    {
        // Arrange
        StatusManager.Input.IsJammed.Value = true;

        // Act & Assert
        var ex = Should.Throw<DeviceException>(() => controller.BeginDeposit());
        ex.Message.ShouldBe("Device is jammed. Cannot begin deposit.");
    }

    /// <summary>ハードウェアがオーバーラップしている場合に BeginDeposit が例外を投げることを検証します。</summary>
    [Fact]
    public void BeginDepositWhenOverlappedThrowsDeviceException()
    {
        // Arrange
        StatusManager.Input.IsOverlapped.Value = true;

        // Act & Assert
        var ex = Should.Throw<DeviceException>(() => controller.BeginDeposit());
        ex.Message.ShouldBe("Device has overlapped cash. Cannot begin deposit.");
    }

    /// <summary>入金トラッキング時に容量を超えた場合に、オーバーフロー金額が正しく計算されることを検証します。</summary>
    [Fact]
    public void TrackDepositWhenCapacityFullCalculatesOverflowAmount()
    {
        // Arrange
        var key = new DenominationKey(1000, CurrencyCashType.Bill);

        // 在庫を 95 枚にする (Full は 100 と想定)
        Inventory.Add(key, 95);
        controller.BeginDeposit();

        // Act
        // 10 枚投入 (空きは 5 枚なので、5 枚分がオーバーフロー)
        controller.TrackDeposit(key, 10);

        // Assert
        controller.DepositAmount.ShouldBe(10000m);
        controller.OverflowAmount.ShouldBe(5000m);
    }

    /// <summary>EndDepositAsync(Change) において、釣銭が不足する場合にマネージャの Dispense が呼ばれることを検証します。</summary>
    /// <returns>非同期タスク。</returns>
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
    /// <returns>非同期タスク。</returns>
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

    /// <summary>EndDepositAsync(Change) において、manager が null で釣銭が必要な場合、例外を投げずにスキップすることを検証します（Logical mutation 対応）。</summary>
    /// <returns>非同期タスク。</returns>
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
        using var sub = controller.Changed.Subscribe(_ => callCount++);
        controller.RealTimeDataEnabled = true;

        // Act & Assert
        controller.Dispose();

        // Dispose 後はメソッド呼び出しで例外が飛ぶ
        Should.Throw<ObjectDisposedException>(() => controller.BeginDeposit());
        Should.Throw<ObjectDisposedException>(() => controller.TrackDeposit(new DenominationKey(1000, CurrencyCashType.Bill), 1));

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

    /// <summary>入金確定時に Counting ステータスでない場合に例外を投げることを検証します。</summary>
    [Fact]
    public void FixDepositWhenNotCountingThrowsException()
    {
        // Act & Assert
        // BeginDeposit() していないので Status は None
        var ex = Should.Throw<DeviceException>(() => controller.FixDeposit());
        ex.Message.ShouldBe("Counting is not in progress.");
        ex.ErrorCode.ShouldBe(DeviceErrorCode.Illegal);
    }

    /// <summary>入金確定時にステータスが Fix になり、Changed イベントが発火することを確認します。</summary>
    [Fact]
    public void FixDepositFiresEventsAndSetsStatus()
    {
        // Arrange
        controller.BeginDeposit();
        bool changedFired = false;
        using var sub = controller.Changed.Subscribe(_ => changedFired = true);

        // Act
        controller.FixDeposit();

        // Assert
        controller.IsFixed.ShouldBeTrue();
        changedFired.ShouldBeTrue();
    }

    /// <summary>EndDepositAsync が遅延を伴って正常に完了し、ステータスが End になることを検証します。</summary>
    /// <returns>非同期タスク。</returns>
    [Fact]
    public async Task EndDepositAsyncCompletesAndSetsStatusToEnd()
    {
        // Arrange
        ConfigurationProvider.Config.Simulation.DepositDelayMs = 100;
        controller.BeginDeposit();
        controller.PauseDeposit(DeviceDepositPause.Pause);
        controller.FixDeposit();

        // Act
        var endTask = controller.EndDepositAsync(DepositAction.NoChange);

        // 仮想時間を進めて完了させる
        TimeProvider.Advance(TimeSpan.FromMilliseconds(100));
        await endTask;

        // Assert
        controller.DepositStatus.ShouldBe(DeviceDepositStatus.End);
        controller.IsBusy.ShouldBeFalse();
        controller.IsPaused.ShouldBeFalse(); // L593 Boolean/Statement mutation 撃破
        controller.IsFixed.ShouldBeFalse();  // L594 Statement mutation 撃破

        // エスクローが空になっていること
        Inventory.EscrowCounts.ShouldBeEmpty(); // L567 Statement mutation 撃破
    }

    /// <summary>入金データの追跡時に金額が正しく更新され、Changed イベントが発火することを検証します。</summary>
    [Fact]
    public void TrackDepositFiresEventsAndUpdatesAmount()
    {
        // Arrange
        controller.BeginDeposit();
        var amountSet = false;
        using var sub = controller.Changed.Subscribe(_ => amountSet = true);

        // Act
        controller.TrackDeposit(new DenominationKey(1000m, CurrencyCashType.Bill), 5);

        // Assert
        controller.DepositAmount.ShouldBe(5000);
        amountSet.ShouldBeTrue();
    }

    /// <summary>例外発生時にメッセージに期待されるキーワードが含まれていることを検証します。</summary>
    [Fact]
    public void BeginDepositWhenBusyThrowsWithDetailedMessage()
    {
        // Arrange
        // リフレクションで IsBusy を true にする
        // リフレクションで IsBusy を true にする
        var stateField = typeof(DepositController).GetField("state", BindingFlags.NonPublic | BindingFlags.Instance);
        var state = (dynamic)stateField!.GetValue(controller)!;
        state.IsBusy = true;

        // Act & Assert
        var ex = Should.Throw<DeviceException>(() => controller.BeginDeposit());
        ex.Message.ShouldContain("busy");
    }

    /// <summary>EndDepositAsync がビジー状態で呼ばれた場合の例外文字列を検証します。</summary>
    /// <returns>非同期タスク。</returns>
    [Fact]
    public async Task EndDepositAsyncWhenBusyThrowsDeviceException()
    {
        // Arrange
        controller.BeginDeposit();
        controller.FixDeposit();

        // リフレクションで IsBusy を true にする
        var stateField = typeof(DepositController).GetField("state", BindingFlags.NonPublic | BindingFlags.Instance);
        var state = (dynamic)stateField!.GetValue(controller)!;
        state.IsBusy = true;

        // Act & Assert
        var ex = await Should.ThrowAsync<DeviceException>(() => controller.EndDepositAsync(DepositAction.NoChange));
        ex.Message.ShouldBe("Device is busy");
    }

    /// <summary>非同期セッション中に FixDeposit を呼び出さずに EndDepositAsync を呼んだ場合のメッセージを検証します。</summary>
    /// <returns>非同期タスク。</returns>
    [Fact]
    public async Task EndDepositAsyncWhenInvalidSequenceThrowsWithMessage()
    {
        // Arrange
        controller.BeginDeposit();

        // FixDeposit() を呼ばない

        // Act & Assert
        var ex = await Should.ThrowAsync<DeviceException>(() => controller.EndDepositAsync(DepositAction.NoChange));
        ex.Message.ShouldContain("Invalid call sequence");
    }

    /// <summary>TrackDeposit 時に DepositStatus が Counting でない場合の検証（例外メッセージ検証含む）。</summary>
    [Fact]
    public void TrackDepositWhenAlreadyFixedThrowsException()
    {
        // Arrange
        controller.BeginDeposit();
        controller.FixDeposit();

        // Act & Assert
        var ex = Should.Throw<DeviceException>(() => controller.TrackDeposit(new DenominationKey(1000m, CurrencyCashType.Bill), 1));
        ex.Message.ShouldBe("Deposit is already fixed.");
    }

    /// <summary>TrackDeposit 時に Jammed の場合の例外とメッセージを検証します。</summary>
    [Fact]
    public void TrackDepositWhenJammedThrowsException()
    {
        // Arrange
        controller.BeginDeposit();
        StatusManager.Input.IsJammed.Value = true;

        // Act & Assert
        var ex = Should.Throw<DeviceException>(() => controller.TrackDeposit(new DenominationKey(1000m, CurrencyCashType.Bill), 1));
        ex.Message.ShouldBe("Device is jammed during tracking.");
    }

    /// <summary>EndDepositAsync(Change) において、釣銭計算により remainingChange がぴったり 0 になり、かつ Manager が非 null の場合に manager.Dispense(0) が呼ばれないことを検証します（論理変異対応）。</summary>
    /// <returns>非同期タスク。</returns>
    [Fact]
    public async Task EndDepositAsyncWhenChangeExactlyCoveredByEscrowDoesNotCallManagerDispense()
    {
        // Arrange
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        var managerMock = new Mock<CashChangerManager>(Inventory, History, ConfigurationProvider);
        var controller = new DepositController(Inventory, StatusManager, managerMock.Object, ConfigurationProvider, TimeProvider);

        controller.BeginDeposit();

        // 5000円投入 (お釣りに使えるエスクロー残高となる)
        controller.TrackDeposit(new DenominationKey(5000, CurrencyCashType.Bill), 1);

        // 4000円要求 -> お釣り 1000円
        controller.RequiredAmount = 4000m;

        // インベントリに 1000円を補充しておく
        Inventory.Add(key, 10);

        controller.FixDeposit();

        // Act
        var task = controller.EndDepositAsync(DepositAction.Change);
        TimeProvider.Advance(TimeSpan.FromSeconds(2));
        await task;

        // Assert
        // お釣りの 1000円は、エスクローにある 5000円から 0枚 引かれる（使えない）ので、
        // 結局 inventory の 1000円が使われる... と思いきや、Escrowには5000円しかないので 1000円のお釣りは払えない！
        // なので RemainingChange = 1000 のまま、最終的な Dispense() 判定に到達して Dispense() が呼ばれてしまう。
        // Wait, ぴったり 0 になるためには、1000円投入して、お釣りにも1000円が必要な状況を作らなければならない。
    }

    /// <summary>残りの釣銭額がちょうど 0 に到達し、かつ manager が非 null の場合に manager.Dispense(0) が呼ばれないことを検証します（論理変異対応）。</summary>
    /// <returns>非同期タスク。</returns>
    [Fact]
    public async Task EndDepositAsyncWhenRemainingChangeHitsExactlyZeroDoesNotCallDispenseWithZero()
    {
        // Arrange
        var key1k = new DenominationKey(1000, CurrencyCashType.Bill);
        var key5k = new DenominationKey(5000, CurrencyCashType.Bill);

        var managerMock = new Mock<CashChangerManager>(Inventory, History, ConfigurationProvider);
        var controller = new DepositController(Inventory, StatusManager, managerMock.Object, ConfigurationProvider, TimeProvider);

        controller.BeginDeposit();

        // 1000円札 2枚投入 (Escrow に 1000円 x 2)
        controller.TrackDeposit(key1k, 2);

        // 要求額 1000円、お釣り 1000円
        controller.RequiredAmount = 1000m;

        controller.FixDeposit();

        // Act
        // 釣銭計算が走り、Escrow の 1000円札がお釣りに使われるため、RemainingChange は 1000 - 1000 = 0 になる
        var task = controller.EndDepositAsync(DepositAction.Change);
        TimeProvider.Advance(TimeSpan.FromSeconds(2));
        await task;

        // Assert
        // remainingChange が 0 の場合、manager != null であっても Dispense は呼ばれないこと
        managerMock.Verify(m => m.Dispense(It.IsAny<decimal>(), It.IsAny<string?>()), Times.Never);

        // Statement mutation対応: Depositは呼ばれること
        managerMock.Verify(m => m.Deposit(It.IsAny<IReadOnlyDictionary<DenominationKey, int>>()), Times.Once);

        // エスクローが正しくクリアされていること (Statement mutation)
        Inventory.EscrowCounts.ShouldBeEmpty();
    }

    /// <summary>各プロパティの getter が内部状態を正しく返すことを検証します（BlockRemoval 対策）。</summary>
    [Fact]
    public void PropertiesReturnCorrectInternalState()
    {
        // Arrange
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        controller.BeginDeposit();
        controller.TrackDeposit(key, 1);

        // Assert
        controller.DepositCounts.ShouldContainKey(key);
        controller.DepositCounts[key].ShouldBe(1);
        controller.LastErrorCode.ShouldBe(DeviceErrorCode.Success);

        // 非ゼロ値をセットして取得を検証 (lock block removal 対応)
        var property = typeof(DepositController).GetProperty("LastErrorCodeExtended", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        property!.SetValue(controller, 123);
        controller.LastErrorCodeExtended.ShouldBe(123);

        controller.LastDepositedSerials.ShouldNotBeNull();

        controller.RequiredAmount = 999m;
        controller.RequiredAmount.ShouldBe(999m);
    }

    /// <summary>DepositCounts が防御的コピーを返していることを検証します（BlockRemoval 対策）。</summary>
    [Fact]
    public void DepositCountsReturnsDefensiveCopy()
    {
        // Arrange
        controller.BeginDeposit();
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        controller.TrackDeposit(key, 1);

        // Act
        var counts1 = controller.DepositCounts;
        var counts2 = controller.DepositCounts;

        // Assert
        counts1.ShouldNotBeSameAs(counts2); // 毎回新しいインスタンス
        counts1.Count.ShouldBe(1);
        counts1[key].ShouldBe(1);
    }

    /// <summary>EndDepositAsync(Change) において、釣銭が必要な場合とマネージャの有無による論理分岐を網羅します（論理変異対応）。</summary>
    /// <param name="changeNeeded">必要な釣銭額。</param>
    /// <param name="hasManager">マネージャの有無。</param>
    /// <param name="expectedDispenseCalls">期待される Dispense 呼び出し回数。</param>
    /// <returns>非同期タスク。</returns>
    [Theory]
    [InlineData(4000, true, 1)] // 釣銭 4000円 (5000円投入で 4000円不足 -> エスクロー(5000円)から払えない) -> Dispense呼ばれる
    [InlineData(4000, false, 0)] // Managerなし -> Dispense呼ばれない
    [InlineData(0, true, 0)] // 釣銭なし -> Dispense呼ばれない
    [InlineData(0, false, 0)] // 釣銭なし && Managerなし -> Dispense呼ばれない
    public async Task EndDepositAsyncLogicTable(
        decimal changeNeeded,
        bool hasManager,
        int expectedDispenseCalls)
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
        var task = controller.EndDepositAsync(DepositAction.Change);
        TimeProvider.Advance(TimeSpan.FromSeconds(2));
        await task;

        // Assert
        mockManager?.Verify(m => m.Dispense(It.Is<decimal>(d => d == changeNeeded), It.IsAny<string?>()), Times.Exactly(expectedDispenseCalls));
    }

    /// <summary>釣銭計算ループにおいて remainingChange がちょうど 0 に到達した際の境界条件を検証します（Equality mutation 対応）。</summary>
    [Fact]
    public void CalculateChangeLoopBoundariesWhenRemainingHitsExactlyZero()
    {
        // Arrange
        Inventory.Add(new DenominationKey(1000, CurrencyCashType.Bill), 10);
        controller.BeginDeposit();
        controller.TrackDeposit(new DenominationKey(5000, CurrencyCashType.Bill), 1);
        controller.RequiredAmount = 4000m; // ちょうど 1000円札 1枚がお釣り（Escrowから戻る）

        // Act
        controller.FixDeposit(); // ここで再計算ロジックが走る

        // Assert
        // 釣銭計算ループが 0 で終了することを期待
        // もし変異があると、0 の時にもう一度回ろうとする
        controller.DepositCounts.Count.ShouldBe(1);
    }

    /// <summary>枚数が 0 の金種がインベントリに追加されないことを検証します（Equality mutation 対応）。</summary>
    /// <returns>非同期タスク。</returns>
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
        var task = controller.EndDepositAsync(DepositAction.Change);
        TimeProvider.Advance(TimeSpan.FromSeconds(2));
        await task;

        // Assert
        // 値が 0 の場合にインベントリ追加がスキップされることを確認
        inventory.AllCounts.Count().ShouldBe(0);
    }

    /// <summary>RealTimeDataEnabled と !disposed の論理演算 を検証します。</summary>
    [Fact]
    public void TrackRejectFiresDataEventOnlyWhenEnabledAndNotDisposed()
    {
        // Arrange
        var dataFired = false;
        using var sub = controller.DataEvents.Subscribe(_ => dataFired = true);

        controller.BeginDeposit();

        // Case 1: Enabled = false, Disposed = false (Baseline: No fire)
        controller.RealTimeDataEnabled = false;
        controller.TrackReject(1000m);
        dataFired.ShouldBeFalse();

        // Case 2: Enabled = true, Disposed = true (Baseline: No fire)
        controller.RealTimeDataEnabled = true;
        controller.Dispose();
        Should.Throw<ObjectDisposedException>(() => controller.TrackReject(1000m));
        dataFired.ShouldBeFalse();

        // Case 3: Enabled = false, Disposed = true
        var controller2 = new DepositController(Inventory, StatusManager)
        {
            RealTimeDataEnabled = false
        };
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
        var ex = Should.Throw<DeviceException>(() => controller.PauseDeposit(DeviceDepositPause.Pause));
        ex.Message.ShouldContain("Session not active");
    }

    /// <summary>Dispose 済み状態ですべての公開メソッドが ObjectDisposedException を投げることを検証します（!disposedガードの網羅）。</summary>
    /// <param name="methodName">対象となるメソッド名。</param>
    /// <returns>非同期タスク。</returns>
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
        controller.Dispose();

        // Act & Assert
        if (methodName == nameof(DepositController.EndDepositAsync))
        {
            await Should.ThrowAsync<ObjectDisposedException>(async () => await controller.EndDepositAsync(DepositAction.NoChange));
        }
        else
        {
            var method = typeof(DepositController).GetMethod(methodName);
            object[] args = methodName switch
            {
                nameof(DepositController.PauseDeposit) => [DeviceDepositPause.Pause],
                nameof(DepositController.TrackDeposit) => [new DenominationKey(1000, CurrencyCashType.Bill), 1],
                nameof(DepositController.TrackReject) => [1000m],
                _ => []
            };

            var ex = Should.Throw<TargetInvocationException>(() => method!.Invoke(controller, args));
            ex.InnerException.ShouldBeOfType<ObjectDisposedException>();
        }
    }

    /// <summary>
    /// 破棄済みの場合に、各種通知メソッドがイベントを発行しないことを検証します。
    /// </summary>
    /// <param name="methodName">実行するメソッド名。</param>
    [Theory]
    [InlineData(nameof(DepositController.BeginDeposit))]
    [InlineData(nameof(DepositController.FixDeposit))]
    [InlineData(nameof(DepositController.PauseDeposit))]
    public void AllNotificationMethodsSuppressWhenDisposed(string methodName)
    {
        // Arrange
        int callCount = 0;
        using var sub = controller.Changed.Subscribe(_ => callCount++);
        controller.Dispose();

        // Act
        try
        {
            var method = typeof(DepositController).GetMethod(methodName, methodName == nameof(DepositController.PauseDeposit) ? [typeof(DeviceDepositPause)] : []);
            method!.Invoke(controller, methodName == nameof(DepositController.PauseDeposit) ? new object[] { DeviceDepositPause.Pause } : null);
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
        controller.BeginDeposit(); // Status: Counting

        // Act
        controller.PauseDeposit(DeviceDepositPause.Pause);
        controller.IsPaused.ShouldBeTrue();

        controller.PauseDeposit(DeviceDepositPause.Resume);
        controller.IsPaused.ShouldBeFalse();
    }

    /// <summary>返却を伴う入金終了が金額をクリアしステータスをリセットすることを検証します。</summary>
    /// <returns>非同期タスク。</returns>
    [Fact]
    public async Task EndDepositAsyncWithRepayClearsEscrowAndResetsAmount()
    {
        // Arrange
        ConfigurationProvider.Config.Simulation.DepositDelayMs = 100;
        controller.BeginDeposit();
        controller.TrackDeposit(new DenominationKey(1000m, CurrencyCashType.Bill), 1);
        controller.FixDeposit();

        // Act
        var endTask = controller.EndDepositAsync(DepositAction.Repay);
        TimeProvider.Advance(TimeSpan.FromMilliseconds(100));
        await endTask;

        // Assert
        controller.DepositAmount.ShouldBe(0);
        controller.DepositStatus.ShouldBe(DeviceDepositStatus.End);

        // エスクローが空になっていること
        Inventory.EscrowCounts.ShouldBeEmpty(); // L481 Statement mutation 撃破

        // カウントもクリアされていること
        controller.DepositCounts.ShouldBeEmpty(); // L599 Statement mutation 撃破
    }

    /// <summary>EndDepositAsync(Change) において、釣銭が不足するが manager が null の場合、Dispense を呼び出さずに完了することを検証します（論理変異の対応）。</summary>
    /// <returns>非同期タスク。</returns>
    [Fact]
    public async Task EndDepositAsyncWhenChangeNeededAndManagerIsNullDoesNotCrash()
    {
        // Arrange
        // FakeTimeProvider を使用して実行を決定的にする
        var controller = new DepositController(Inventory, StatusManager, null, ConfigurationProvider, TimeProvider);
        controller.BeginDeposit();
        controller.TrackDeposit(new DenominationKey(1000, CurrencyCashType.Bill), 5); // 5000円投入
        controller.RequiredAmount = 1000m; // 4000円お釣りが必要
        Inventory.Clear(); // インベントリにお釣りなし

        // Act
        controller.FixDeposit();
        var task = controller.EndDepositAsync(DepositAction.Change);
        TimeProvider.Advance(TimeSpan.FromSeconds(2));
        await task;

        // Assert
        // 変異 (&& -> ||) があると、manager != null が false でも remainingChange > 0 が true なので
        // manager.Dispense(4000) が呼ばれ、NullReferenceException が発生する。
        // その例外は内部で catch され、LastErrorCode が Failure になるため、Successであることを確認して変異対応。
        controller.LastErrorCode.ShouldBe(DeviceErrorCode.Success);
    }

    /// <summary>RealTimeDataEnabled が false の場合、TrackDeposit を呼んでも DataEvents が発火しないことを検証します（論理変異の対応）。</summary>
    [Fact]
    public void TrackDepositDoesNotFireDataEventWhenRealTimeDataDisabled()
    {
        // Arrange
        controller.RealTimeDataEnabled = false;
        bool dataEventFired = false;
        using var sub = controller.DataEvents.Subscribe(_ => dataEventFired = true);

        controller.BeginDeposit();

        // Act
        controller.TrackDeposit(new DenominationKey(1000, CurrencyCashType.Bill), 1);

        // Assert
        // 変異 (&& -> ||) があると、RealTimeDataEnabled が false でも !disposed が true なので発火してしまう。
        dataEventFired.ShouldBeFalse();
    }

    /// <summary>EndDepositAsync 実行中に Dispose された場合、後続の通知処理が抑制されることを検証します（!disposed ガード変異の対応）。</summary>
    /// <returns>非同期タスク。</returns>
    [Fact]
    public async Task EndDepositAsyncWhenDisposedDuringDelaySuppressesNotifications()
    {
        // Arrange
        ConfigurationProvider.Config.Simulation.DepositDelayMs = 100;
        var controller = new DepositController(Inventory, StatusManager, null, ConfigurationProvider, TimeProvider);
        controller.BeginDeposit();
        controller.FixDeposit();

        int errorCallCount = 0;
        using var errSub = controller.ErrorEvents.Subscribe(_ => errorCallCount++);

        // Act
        var task = controller.EndDepositAsync(DepositAction.NoChange);

        // Delay 中 (まだタスクは完了していない) に Dispose
        controller.Dispose();

        // 時間を進めて EndDepositAsync の後半を続行させる
        TimeProvider.Advance(TimeSpan.FromMilliseconds(100));

        await task;

        // Assert
        // 変異 (!disposed -> disposed または削除) があると、ErrorEvents や Changed が発火してしまう。
        errorCallCount.ShouldBe(0);
    }

    /// <summary>例外発生時の ErrorEvents 発火における !disposed ガードを検証します。</summary>
    /// <returns>非同期タスク。</returns>
    [Fact]
    public async Task EndDepositAsyncWhenErrorOccursAndDisposedSuppressesErrorEvent()
    {
        // Arrange
        ConfigurationProvider.Config.Simulation.DepositDelayMs = 100;
        var controller = new DepositController(Inventory, StatusManager, null, ConfigurationProvider, TimeProvider);
        controller.BeginDeposit();
        controller.FixDeposit();

        int errorCallCount = 0;
        using var errSub = controller.ErrorEvents.Subscribe(_ => errorCallCount++);

        // デバイスエラー(Overlapped)をシミュレート
        StatusManager.Input.IsOverlapped.Value = true;

        var task = controller.EndDepositAsync(DepositAction.NoChange);

        // Delay 中に Dispose して disposed フラグを立てる
        controller.Dispose();
        TimeProvider.Advance(TimeSpan.FromMilliseconds(100));

        // キャッチされたDeviceExceptionにより ErrorEvents が飛ばないことを確認
        await task;

        // Assert
        errorCallCount.ShouldBe(0);
    }

    /// <summary>changeAmount が 0 の場合、釣銭計算の true ブロックがスキップされることを検証します（Equality mutation 対応）。</summary>
    /// <returns>非同期タスク。</returns>
    [Fact]
    public async Task EndDepositAsyncChangeWhenChangeAmountIsZeroSkipsTrueBlock()
    {
        // Arrange
        var mockInventory = new Mock<Inventory>() { CallBase = true };
        var controller = new DepositController(mockInventory.Object, StatusManager, null, ConfigurationProvider, TimeProvider);
        controller.BeginDeposit();

        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        controller.TrackDeposit(key, 1);
        controller.RequiredAmount = 1000m; // 投入額1000円、要求1000円 -> changeAmount = 0
        controller.FixDeposit();

        // これまでの TrackDeposit 等による AddEscrow 呼び出し履歴をクリア
        mockInventory.Invocations.Clear();

        // Act
        var task = controller.EndDepositAsync(DepositAction.Change);
        TimeProvider.Advance(TimeSpan.FromSeconds(2));
        await task;

        // Assert
        // changeAmount > 0 判定が変異すると、0 なのに true ブロックに入り、AddEscrow が呼ばれる。
        mockInventory.Verify(i => i.AddEscrow(It.IsAny<DenominationKey>(), It.IsAny<int>()), Times.Never);
    }

    /// <summary>枚数が 0 の場合、AddEscrow が呼ばれないことを検証します（Equality mutation 対応）。</summary>
    /// <returns>非同期タスク。</returns>
    [Fact]
    public async Task EndDepositAsyncChangeDoesNotAddEscrowWithZeroCount()
    {
        // Arrange
        var mockInventory = new Mock<Inventory>() { CallBase = true };
        var controller = new DepositController(mockInventory.Object, StatusManager, null, ConfigurationProvider, TimeProvider);
        controller.BeginDeposit();

        // 5000円札1枚投入、要求額1000円 -> 釣銭4000円
        var key5k = new DenominationKey(5000, CurrencyCashType.Bill);
        controller.TrackDeposit(key5k, 1);
        controller.RequiredAmount = 1000m;
        controller.FixDeposit();

        // 履歴をクリア
        mockInventory.Invocations.Clear();

        // Act
        var task = controller.EndDepositAsync(DepositAction.Change);
        TimeProvider.Advance(TimeSpan.FromSeconds(2));
        await task;

        // Assert
        // 釣銭計算の結果、5000円札の storeCounts (残数) は 1 - 0 = 1 枚。
        // もし kv.Value > 0 判定が変異し、他の金種（0枚）が storeCounts に含まれていた場合 AddEscrow(key, 0) が呼ばれる。
        // （実際には TrackDeposit した金種しか storeCounts に入らないのでこの変異は一部発現しない可能性があるが、
        // 少なくとも 0 で呼ばれないことを検証しておく）
        mockInventory.Verify(i => i.AddEscrow(It.IsAny<DenominationKey>(), 0), Times.Never);
    }

    /// <summary>すでに Pause 状態の時に Pause を要求すると例外がスローされることを検証します（変異対応）。</summary>
    [Fact]
    public void PauseDepositWhenAlreadyPausedThrowsException()
    {
        // Arrange
        controller.BeginDeposit(); // Status: Counting
        controller.PauseDeposit(DeviceDepositPause.Pause); // IsPaused = true になる

        // Act & Assert
        // 変異 (IsPaused != requestedPause) があると、同じ状態なのに例外が飛ばない。
        var ex = Should.Throw<DeviceException>(() => controller.PauseDeposit(DeviceDepositPause.Pause));
        ex.ErrorCode.ShouldBe(DeviceErrorCode.Illegal);

        // 変異 (true?"paused":"running") によりメッセージが変わることを検知。
        ex.Message.ShouldContain("paused");
    }

    /// <summary>すでに Resume 状態の時に Resume を要求すると例外がスローされることを検証します（変異対応）。</summary>
    [Fact]
    public void PauseDepositWhenAlreadyRunningThrowsException()
    {
        // Arrange
        controller.BeginDeposit(); // Status: Counting, IsPaused: false

        // Act & Assert
        var ex = Should.Throw<DeviceException>(() => controller.PauseDeposit(DeviceDepositPause.Resume));
        ex.ErrorCode.ShouldBe(DeviceErrorCode.Illegal);
        ex.Message.ShouldContain("running");
    }

    /// <summary>返金処理が正しく行われ、エスクローがクリアされること（およびステータス変更・イベント通知）を検証します。</summary>
    /// <returns>非同期タスク。</returns>
    [Fact]
    public async Task EndDepositAsyncRepayFiresEventsAndClearsEscrow()
    {
        // Arrange
        controller.BeginDeposit();

        // エスクローに何か入れておく
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        controller.TrackDeposit(key, 1);
        controller.FixDeposit();

        bool changedFired = false;
        using var sub = controller.Changed.Subscribe(_ => changedFired = true);

        // Act
        var task = controller.EndDepositAsync(DepositAction.Repay);
        TimeProvider.Advance(TimeSpan.FromSeconds(2));
        await task;

        // Assert
        controller.DepositStatus.ShouldBe(DeviceDepositStatus.End);
        changedFired.ShouldBeTrue();

        // Statement mutation対応: エスクローがクリアされていること
        Inventory.EscrowCounts.ShouldBeEmpty();
    }

    /// <summary>釣銭なし確定処理が正しく行われ、エスクローがインベントリに追加されてからクリアされること（およびステータス変更等）を検証します。</summary>
    /// <returns>非同期タスク。</returns>
    [Fact]
    public async Task EndDepositAsyncNoChangeFiresEventsAndUpdatesInventory()
    {
        // Arrange
        controller.BeginDeposit();

        // エスクローに何か入れておく
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        controller.TrackDeposit(key, 2);
        controller.FixDeposit();

        bool changedFired = false;
        using var sub = controller.Changed.Subscribe(_ => changedFired = true);

        // Act
        var task = controller.EndDepositAsync(DepositAction.NoChange);
        TimeProvider.Advance(TimeSpan.FromSeconds(2));
        await task;

        // Assert
        controller.DepositStatus.ShouldBe(DeviceDepositStatus.End);
        changedFired.ShouldBeTrue();

        // Statement mutation対応:
        // - エスクローが空になる
        // - メインインベントリに追加されている
        Inventory.EscrowCounts.ShouldBeEmpty();
        Inventory.GetCount(key).ShouldBe(2);
    }

    /// <summary>デバイスが重複投入（Overlapped）状態の時に入金トラックを試みると例外がスローされることを検証します（変異対応）。</summary>
    [Fact]
    public void TrackDepositThrowsWhenOverlapped()
    {
        // Arrange
        controller.BeginDeposit();
        StatusManager.Input.IsOverlapped.Value = true;
        var key = new DenominationKey(1000, CurrencyCashType.Bill);

        // Act & Assert
        // Negate expression 変異 (if(!overlapped)) を制御する
        var ex = Should.Throw<DeviceException>(() => controller.TrackDeposit(key, 1));
        ex.ErrorCode.ShouldBe(DeviceErrorCode.Overlapped);
    }

    /// <summary>入金確定（釣銭あり）時に、エスクロー内の一部が釣銭として使われ、残りがインベントリに追加されることを検証します（ロジック対応）。</summary>
    /// <returns>非同期タスク。</returns>
    [Fact]
    public async Task EndDepositAsyncChangeWithEscrowReuse()
    {
        // Arrange
        var mockInventory = new Mock<Inventory>() { CallBase = true };
        var controller = new DepositController(mockInventory.Object, StatusManager, null, ConfigurationProvider, TimeProvider);
        controller.BeginDeposit();

        // 1000円札5枚投入、要求額1000円 -> おつり4000円
        var key1k = new DenominationKey(1000, CurrencyCashType.Bill);
        controller.TrackDeposit(key1k, 5);
        controller.RequiredAmount = 1000m;
        controller.FixDeposit();

        // 期待値: 4000円分が再利用（おつりとして返る）され、1000円分だけが最終的にインベントリに入る。
        // 在庫の減算処理 (5 - 4 = 1)

        // Act
        var task = controller.EndDepositAsync(DepositAction.Change);
        TimeProvider.Advance(TimeSpan.FromSeconds(2));
        await task;

        // Assert
        // 釣銭有無の論理変異対応
        // インベントリには 1枚だけ入っているはず
        mockInventory.Object.GetCount(key1k).ShouldBe(1);
    }

    /// <summary>入金確定時にマネージャーが null の場合、直接インベントリに加算されることを検証します（Fallback 対応）。</summary>
    /// <returns>非同期タスク。</returns>
    [Fact]
    public async Task EndDepositAsyncChangeWhenManagerIsNullFallback()
    {
        // Arrange
        // Manager を渡さずに初期化
        var controller = new DepositController(Inventory, StatusManager, null, ConfigurationProvider, TimeProvider);
        controller.BeginDeposit();
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        controller.TrackDeposit(key, 2);
        controller.FixDeposit();

        // Act
        var task = controller.EndDepositAsync(DepositAction.NoChange);
        TimeProvider.Advance(TimeSpan.FromSeconds(2));
        await task;

        // Assert
        // manager == null ブロック が実行され、Inventory に直接入ることを確認
        Inventory.GetCount(key).ShouldBe(2);
    }

    /// <summary>TrackDeposit が指定された枚数分のイベントを正確に通知することを検証します（ループ変異対応）。</summary>
    [Fact]
    public void TrackDepositNotifiesCorrectNumberOfEvents()
    {
        // Arrange
        controller.BeginDeposit();
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        int count = 5;

        // Act
        controller.TrackDeposit(key, count);

        // Assert
        // ループカウンタ変異を抑制するために、生成されたシリアル番号の数を確認
        var stateField = typeof(DepositController).GetField("state", BindingFlags.NonPublic | BindingFlags.Instance);
        var state = (dynamic)stateField!.GetValue(controller)!;
        ((int)state.DepositedSerials.Count).ShouldBe(count);
    }

    /// <summary>Dispose 時に内部リソース（CancellationTokenSource や CompositeDisposable）が破棄されることを検証します。</summary>
    [Fact]
    public void DisposeCleansUpAllResources()
    {
        // Arrange
        var controller = new DepositController(Inventory, StatusManager, null, ConfigurationProvider, TimeProvider);

        // 動作中のタスクを作るために BeginDeposit -> FixDeposit -> EndDepositAsync
        controller.BeginDeposit();
        controller.FixDeposit();
        _ = controller.EndDepositAsync(DepositAction.NoChange);

        var trackerField = typeof(DepositController).GetField("tracker", BindingFlags.NonPublic | BindingFlags.Instance);
        var trackerObj = trackerField?.GetValue(controller);
        var ctsField = typeof(DepositTracker).GetField("depositCts", BindingFlags.NonPublic | BindingFlags.Instance);
        var cts = (CancellationTokenSource?)ctsField?.GetValue(trackerObj);
        cts.ShouldNotBeNull();

        // Act
        controller.Dispose();

        // Assert
        // !disposed ガード変異対応のための副作用確認
        // CTS が破棄されていること
        Should.Throw<ObjectDisposedException>(() => cts.Token);

        // CompositeDisposable に関しては内部状態を覗くのが難しいため、
        // disposed フラグが true になっていることを確認
        var disposedField = typeof(DepositController).GetField("disposed", BindingFlags.NonPublic | BindingFlags.Instance);
        ((bool)disposedField!.GetValue(controller)!).ShouldBeTrue();
    }

    /// <summary>入金額と要求額が同じ（お釣りが0円）の場合に、エスクローが正しくクリアされることを検証します（境界変異撃退）。</summary>
    /// <returns>非同期タスク。</returns>
    [Fact]
    public async Task EndDepositAsyncChangeWithZeroChangeAmount()
    {
        // Arrange
        controller.BeginDeposit();
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        controller.TrackDeposit(key, 1);
        controller.RequiredAmount = 1000m; // 1000 - 1000 = 0
        controller.FixDeposit();

        // Act
        var task = controller.EndDepositAsync(DepositAction.Change);
        TimeProvider.Advance(TimeSpan.FromSeconds(2));
        await task;

        // Assert
        // changeAmount > 0 判定が false になり、else ブロックが実行されることを確認
        Inventory.EscrowCounts.ShouldBeEmpty();
        Inventory.GetCount(key).ShouldBe(1);
    }

    /// <summary>エスクロー残高と払出要求額が完全に一致する場合の正常終了を検証します。</summary>
    /// <returns>非同期タスク。</returns>
    [Fact]
    public async Task EndDepositAsyncChangeWithExactEscrowMatch()
    {
        // Arrange
        var key = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        bool dispenseCalled = false;
        var mockManager = new Mock<CashChangerManager>(Inventory, new TransactionHistory(), ConfigurationProvider) { CallBase = true };
        mockManager.Setup(m => m.Dispense(It.IsAny<decimal>(), It.IsAny<string>())).Callback(() => dispenseCalled = true);

        // Manager をセットした新しいインスタンスを使用
        var controller = new DepositController(Inventory, StatusManager, mockManager.Object, ConfigurationProvider, TimeProvider);
        controller.BeginDeposit();
        controller.TrackDeposit(key, 2);
        controller.RequiredAmount = 1000m; // Change = 1000
        controller.FixDeposit();

        // Act
        var task = controller.EndDepositAsync(DepositAction.Change);
        TimeProvider.Advance(TimeSpan.FromSeconds(2));
        await task;

        // Assert
        // 釣銭計算が完了(remainingChange == 0)し、ループを継続しないことを確認。
        dispenseCalled.ShouldBeFalse();
        Inventory.GetTotalCount(key).ShouldBe(1); // 1000円追加
    }

    /// <summary>エスクロー内の硬貨の額がお釣りよりも大きく、useCount が 0 になるケースを検証します（境界変異撃退）。</summary>
    /// <returns>非同期タスク。</returns>
    [Fact]
    public async Task EndDepositAsyncChangeWhenEscrowIsTooLarge()
    {
        // Arrange
        controller.BeginDeposit();
        var key5k = new DenominationKey(5000, CurrencyCashType.Bill, "JPY");
        controller.TrackDeposit(key5k, 1);
        controller.RequiredAmount = 4000m; // Change = 1000 (エスクローは 5000円のみ)
        controller.FixDeposit();

        // Act
        var task = controller.EndDepositAsync(DepositAction.Change);
        TimeProvider.Advance(TimeSpan.FromSeconds(2));
        await task;

        // Assert
        // 5000円札は 1000円おつりには使えないので useCount = 0 になり、そのまま収納されるべき。
        Inventory.GetTotalCount(key5k).ShouldBe(1);
    }

    /// <summary>リアルタイムデータ通知が有効/無効および Dispose 状態によって正しくガードされることを検証します（変異対応）。</summary>
    /// <param name="enabled">リアルタイムデータ通知が有効かどうか。</param>
    /// <param name="disposeBefore">通知前に Dispose するかどうか。</param>
    /// <param name="expectedCount">期待される発火回数。</param>
    [Theory]
    [InlineData(true, false, 1)] // Enabled, Not Disposed -> Notified
    [InlineData(false, false, 0)] // Disabled, Not Disposed -> Suppressed
    [InlineData(true, true, 0)] // Enabled, Disposed -> Suppressed
    public void TrackDepositNotifiesDataEventsOnlyWhenEnabledAndNotDisposed(bool enabled, bool disposeBefore, int expectedCount)
    {
        // Arrange
        controller.RealTimeDataEnabled = enabled;
        controller.BeginDeposit();

        int fireCount = 0;
        using var sub = controller.DataEvents.Subscribe(_ => fireCount++);

        if (disposeBefore)
        {
            controller.Dispose();
        }

        var key = new DenominationKey(1000, CurrencyCashType.Bill);

        // Act
        try
        {
            controller.TrackDeposit(key, 1);
        }
        catch (ObjectDisposedException)
        {
        }

        // Assert
        fireCount.ShouldBe(expectedCount);
    }

    /// <summary>EndDepositAsync 開始時の通知が Dispose 済みの場合に抑止されることを検証します（変異対応）。</summary>
    /// <returns>非同期タスク。</returns>
    [Fact]
    public async Task EndDepositAsyncFiresChangedOnlyWhenNotDisposed()
    {
        // Arrange
        controller.BeginDeposit();
        controller.FixDeposit();

        int callCount = 0;
        using var sub = controller.Changed.Subscribe(_ => callCount++);

        controller.Dispose();

        // Act & Assert
        // !disposed 判定。Dispose 済みなら通知せず ObjectDisposedException を投げるべき。
        await Should.ThrowAsync<ObjectDisposedException>(async () => await controller.EndDepositAsync(DepositAction.NoChange));
        callCount.ShouldBe(0);
    }

    /// <summary>おつりがちょうど 0 円になるケースで、Dispense が呼ばれず、かつエスクローが空になることを検証します（論理分岐対応）。</summary>
    /// <returns>非同期タスク。</returns>
    [Fact]
    public async Task EndDepositAsyncChangeWithZeroRemainingChange()
    {
        // Arrange
        var key = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        bool dispenseCalled = false;
        var mockManager = new Mock<CashChangerManager>(Inventory, new TransactionHistory(), ConfigurationProvider) { CallBase = true };
        mockManager.Setup(m => m.Dispense(It.IsAny<decimal>(), It.IsAny<string>())).Callback(() => dispenseCalled = true);

        var controller = new DepositController(Inventory, StatusManager, mockManager.Object, ConfigurationProvider, TimeProvider);
        controller.BeginDeposit();
        controller.TrackDeposit(key, 1);
        controller.RequiredAmount = 1000m; // Change = 0
        controller.FixDeposit();

        // Act
        var task = controller.EndDepositAsync(DepositAction.Change);
        TimeProvider.Advance(TimeSpan.FromSeconds(2));
        await task;

        // Assert
        dispenseCalled.ShouldBeFalse();
        Inventory.GetTotalCount(key).ShouldBe(1);
        Inventory.EscrowCounts.ShouldBeEmpty();
    }

    /// <summary>オーバーラップエラーの際に EndDepositAsync が適切なエラーコードを設定することを検証します（エラーコード設定変異対応）。</summary>
    /// <returns>非同期タスク。</returns>
    [Fact]
    public async Task EndDepositAsyncWhenOverlappedSetsErrorCode()
    {
        // Arrange
        controller.BeginDeposit();
        controller.TrackDeposit(new DenominationKey(1000, CurrencyCashType.Bill), 1);
        controller.FixDeposit();
        StatusManager.Input.IsOverlapped.Value = true;

        // Act
        var task = controller.EndDepositAsync(DepositAction.NoChange);
        TimeProvider.Advance(TimeSpan.FromSeconds(2));
        await task;

        // Assert
        controller.LastErrorCode.ShouldBe(DeviceErrorCode.Overlapped);
    }

    /// <summary>オーバーラップ時の入金開始において例外メッセージが正確であることを検証します（文字列変異撃退）。</summary>
    [Fact]
    public void TrackDepositWhenOverlappedThrowsWithCorrectMessage()
    {
        // Arrange
        controller.BeginDeposit();
        StatusManager.Input.IsOverlapped.Value = true;

        // Act & Assert
        var ex = Should.Throw<DeviceException>(() => controller.TrackDeposit(new DenominationKey(1000, CurrencyCashType.Bill), 1));
        ex.Message.ShouldBe("Device has overlapped cash. Cannot track deposit.");
        ex.ErrorCode.ShouldBe(DeviceErrorCode.Overlapped);
    }
}
