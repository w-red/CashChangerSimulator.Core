using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Device.Virtual;
using Microsoft.Extensions.Logging;
using PosSharp.Abstractions;
using Shouldly;

namespace CashChangerSimulator.Tests.Device.Virtual;

/// <summary>VirtualCashChangerDevice の機能検証テスト</summary>
[Collection("SequentialHardwareTests")]
public class VirtualCashChangerDeviceTests : DeviceTestBase
{
    private readonly ICashChangerDevice device1;
    private readonly ICashChangerDevice device2;

    /// <summary>テスト用のインスタンスを初期化します</summary>
    public VirtualCashChangerDeviceTests()
    {
        // 各テストで共有の Mutex 名前空間を使用して競合を避ける
        var testMutexName = GenerateUniqueMutexName();
        device1 = CreateDevice(testMutexName);
        device2 = CreateDevice(testMutexName);
    }

    /// <summary>複数のインスタンスで同時に排他権(Claim)を取得しようとした場合に例外が発生することを確認します</summary>
    [Fact]
    public async Task ConcurrentClaimShouldThrowException()
    {
        // Arrange
        await device1.OpenAsync();
        await device2.OpenAsync();

        // Act & Assert
        await device1.ClaimAsync(TestTimingConstants.ShortDelayMs);

        // 別タスクで device2.Claim を実行し、例外を確認する
        var task = Task.Run(() => device2.ClaimAsync(TestTimingConstants.ShortDelayMs));

        // Assert: 別タスクからの Claim は失敗するため
        await Should.ThrowAsync<Exception>(async () => await task.WaitAsync(TimeSpan.FromMilliseconds(TestTimingConstants.DefaultTimeoutMs)));
    }

    /// <summary>排他権を解放した後に別のインスタンスが排他権を取得できることを確認します</summary>
    [Fact]
    public async Task ClaimAfterReleaseShouldSucceed()
    {
        // Arrange
        await device1.OpenAsync();
        await device2.OpenAsync();

        // Act
        await device1.ClaimAsync(TestTimingConstants.ShortDelayMs);
        await device1.ReleaseAsync();

        // Assert
        await device2.ClaimAsync(TestTimingConstants.ShortDelayMs);

        // Exception が投げられないことで成功を確認
    }

    /// <summary>デバイスをオープンした際、接続状態が正しく更新されることを確認します</summary>
    [Fact]
    public async Task OpenShouldSetConnected()
    {
        await device1.OpenAsync();
        StatusManager.IsConnected.CurrentValue.ShouldBeTrue();
        device1.State.CurrentValue.ShouldBe(PosSharp.Abstractions.ControlState.Idle);
    }

    /// <summary>デバイスをクローズした際、切断状態および無効状態になることを確認します</summary>
    [Fact]
    public async Task CloseShouldSetDisconnectedAndDisabled()
    {
        await device1.OpenAsync();
        await device1.ClaimAsync(TestTimingConstants.ShortDelayMs);
        await device1.EnableAsync();

        await device1.CloseAsync();

        StatusManager.IsConnected.CurrentValue.ShouldBeFalse();
        StatusManager.DeviceEnabled.CurrentValue.ShouldBeFalse();
        device1.State.CurrentValue.ShouldBe(PosSharp.Abstractions.ControlState.Closed);
    }

    /// <summary>排他権取得済みの状態でデバイスを有効化できることを確認します</summary>
    [Fact]
    public async Task EnableShouldSucceedWhenClaimed()
    {
        await device1.OpenAsync();
        await device1.ClaimAsync(TestTimingConstants.ShortDelayMs);
        await device1.EnableAsync();
        StatusManager.DeviceEnabled.CurrentValue.ShouldBeTrue();
        device1.State.CurrentValue.ShouldBe(PosSharp.Abstractions.ControlState.Idle);
    }

    /// <summary>排他権を取得していない状態でデバイスを有効化しようとした場合に、正しいエラーコードで例外が発生することを確認します</summary>
    [Fact]
    public async Task EnableShouldThrowWhenNotClaimed()
    {
        await device1.OpenAsync();
        var ex = await Should.ThrowAsync<DeviceException>(device1.EnableAsync);
        ex.ErrorCode.ShouldBe(DeviceErrorCode.Illegal);
    }

    /// <summary>オープンしていない状態で排他権を取得しようとした場合に、正しいエラーコードで例外が発生することを確認します</summary>
    [Fact]
    public async Task ClaimShouldThrowWhenNotOpened()
    {
        // OpenAsync を呼ばずに ClaimAsync を実行
        var ex = await Should.ThrowAsync<DeviceException>(() => device1.ClaimAsync(0));
        ex.ErrorCode.ShouldBe(DeviceErrorCode.Closed);
    }

    /// <summary>デバイスが無効な状態で入金を開始しようとした場合に例外が発生することを確認します</summary>
    [Fact]
    public async Task DepositShouldThrowWhenNotEnabled()
    {
        await device1.OpenAsync();
        await device1.ClaimAsync(TestTimingConstants.ShortDelayMs);

        // Not enabled
        await Should.ThrowAsync<DeviceException>(device1.BeginDepositAsync);
    }

    /// <summary>デバイスが無効な状態で出金を開始しようとした場合に例外が発生することを確認します</summary>
    [Fact]
    public async Task DispenseShouldThrowWhenNotEnabled()
    {
        await device1.OpenAsync();
        await device1.ClaimAsync(TestTimingConstants.ShortDelayMs);

        // Not enabled
        await Should.ThrowAsync<DeviceException>(async () => await device1.DispenseChangeAsync(1000));
    }

    /// <summary>在庫情報の読み取りが空でないインベントリを返すことを確認します</summary>
    [Fact]
    public async Task ReadInventoryShouldReturnCorrectData()
    {
        await device1.OpenAsync();
        await device1.ClaimAsync(TestTimingConstants.ShortDelayMs);
        await device1.EnableAsync();

        // DepositController 経由で預け入れを行う(VirtualCashChangerDevice のメソッドを使用)
        await device1.BeginDepositAsync();

        var inventory = await device1.ReadInventoryAsync();
        inventory.ShouldNotBeNull();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            device1.Dispose();
            device2.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <summary>DirectIO を使用して出金口の現金取得とクリアができることを確認します。</summary>
    [Fact]
    public async Task DirectIOShouldHandleTakeCashAndGetCounts()
    {
        await device1.OpenAsync();
        await device1.ClaimAsync(TestTimingConstants.ShortDelayMs);
        await device1.EnableAsync();

        var bill = new DenominationKey(1000, CurrencyCashType.Bill);
        var counts = new Dictionary<DenominationKey, int> { { bill, 2 } };

        // 1. 出金完了をシミュレートして枚数を追加
        StatusManager.Input.AddExitPortCounts(ExitPort.Normal, counts);

        // 2. DirectIO (GetExitPortCounts) で枚数取得
        var resultDict = new Dictionary<DenominationKey, int>();
        await device1.DirectIOAsync(DirectIOCommands.GetExitPortCounts, (int)ExitPort.Normal, resultDict);
        resultDict[bill].ShouldBe(2);

        // 3. DirectIO (TakeCash) で現金回収
        await device1.DirectIOAsync(DirectIOCommands.TakeCash, (int)ExitPort.Normal, null!);
        
        // 4. クリアされていることを確認
        await device1.DirectIOAsync(DirectIOCommands.GetExitPortCounts, (int)ExitPort.Normal, resultDict);
        resultDict.ShouldBeEmpty();
    }

    /// <summary>コンストラクタに null を渡した際、ArgumentNullException が発生することを確認します。</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void Constructor_ShouldThrowArgumentNullException_WhenRequiredArgumentIsNull(int nullParamIndex)
    {
        // 依存オブジェクトの準備
        var dc = new DepositController(Manager, Inventory, StatusManager, ConfigurationProvider, LoggerFactory);
        var dpc = new DispenseController(Manager, Inventory, ConfigurationProvider, LoggerFactory, StatusManager, HardwareSimulator.Create());
        var diag = new DiagnosticController(Inventory, StatusManager);
        var logger = LoggerFactory.CreateLogger<VirtualCashChangerDevice>();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
        {
            _ = new VirtualCashChangerDevice(
                nullParamIndex == 0 ? null! : dc,
                nullParamIndex == 1 ? null! : dpc,
                nullParamIndex == 2 ? null! : diag,
                nullParamIndex == 3 ? null! : StatusManager,
                nullParamIndex == 4 ? null! : Manager,
                nullParamIndex == 5 ? null! : Inventory,
                nullParamIndex == 6 ? null! : logger,
                "TestMutex");
        });
    }

    /// <summary>前のプロセスが Mutex を解放せずに終了(Abandoned)した場合でも、ClaimAsync が成功することを確認します。</summary>
    [Fact]
    public async Task ClaimAsync_ShouldSucceed_WhenMutexIsAbandoned()
    {
        // Arrange
        var mutexName = GenerateUniqueMutexName();
        var abandonedMutex = new Mutex(false, mutexName);
        
        // 別スレッドで Mutex を取得し、解放せずにスレッドを終了させる
        var thread = new Thread(() =>
        {
            abandonedMutex.WaitOne();
            // Thread finishes without releasing
        });
        thread.Start();
        thread.Join();

        // Act
        var device = CreateDevice(mutexName);
        await device.OpenAsync();

        // Assert: AbandonedMutexException が内部でキャッチされ、成功すること
        await Should.NotThrowAsync(() => device.ClaimAsync(TestTimingConstants.ShortDelayMs));
        
        device.Dispose();
    }

    /// <summary>初期状態のプロパティ値を確認します。</summary>
    [Fact]
    public void InitialState_ShouldBeCorrect()
    {
        device1.IsBusy.CurrentValue.ShouldBeFalse();
        device1.State.CurrentValue.ShouldBe(ControlState.Closed);
    }

    /// <summary>デバイスをクローズした際の状態遷移を確認します。</summary>
    [Fact]
    public async Task CloseAsync_ShouldResetState()
    {
        await device1.OpenAsync();
        await device1.ClaimAsync(0);
        await device1.EnableAsync();
        device1.State.CurrentValue.ShouldBe(ControlState.Idle);

        await device1.CloseAsync();
        device1.State.CurrentValue.ShouldBe(ControlState.Closed);
        
        // 再度 Open できること
        await device1.OpenAsync();
        device1.State.CurrentValue.ShouldBe(ControlState.Idle);
    }

    /// <summary>デバイスを無効化した際の状態遷移を確認します。</summary>
    [Fact]
    public async Task DisableAsync_ShouldChangeStateToIdle()
    {
        await device1.OpenAsync();
        await device1.ClaimAsync(0);
        await device1.EnableAsync();
        
        await device1.DisableAsync();
        device1.State.CurrentValue.ShouldBe(ControlState.Idle);
        StatusManager.DeviceEnabled.CurrentValue.ShouldBeFalse();
    }

    /// <summary>入金操作の委譲を確認します。</summary>
    [Fact]
    public async Task DepositOperations_ShouldBeDelegated()
    {
        await device1.OpenAsync();
        await device1.ClaimAsync(0);
        await device1.EnableAsync();

        // Inventory setup
        var bill1000 = new DenominationKey(1000, CurrencyCashType.Bill);
        Inventory.SetCount(bill1000, 10);

        // Begin
        await device1.BeginDepositAsync();
        device1.IsBusy.CurrentValue.ShouldBeTrue();
        device1.State.CurrentValue.ShouldBe(ControlState.Busy);

        // Pause
        await device1.PauseDepositAsync(DeviceDepositPause.Pause);
        // (ロジック上、Pause 中も Busy)

        // Fix
        // Set delay to test busy during Fix
        ConfigurationProvider.Config.Simulation.DepositDelayMs = 100;
        var fixTask = device1.FixDepositAsync();
        device1.IsBusy.CurrentValue.ShouldBeTrue();
        await WaitUntil(() => fixTask.IsCompleted);
        await fixTask;

        // End
        var endTask = device1.EndDepositAsync(DepositAction.NoChange);
        await WaitUntil(() => endTask.IsCompleted);
        await endTask;

        device1.IsBusy.CurrentValue.ShouldBeFalse();
        device1.State.CurrentValue.ShouldBe(ControlState.Idle);
    }

    /// <summary>出金操作の委譲を確認します。</summary>
    [Fact]
    public async Task DispenseOperations_ShouldBeDelegated()
    {
        await device1.OpenAsync();
        await device1.ClaimAsync(0);
        await device1.EnableAsync();

        // 準備（在庫追加）
        var bill = new DenominationKey(1000, CurrencyCashType.Bill);
        Manager.Adjust(new Dictionary<DenominationKey, int> { { bill, 10 } });

        // Inventory setup
        var bill1000 = new DenominationKey(1000, CurrencyCashType.Bill);
        Inventory.SetCount(bill1000, 10);

        // Non-zero delay for busy check
        ConfigurationProvider.Config.Simulation.DispenseDelayMs = 100;

        // DispenseChange
        var task = device1.DispenseChangeAsync(1000);
        device1.IsBusy.CurrentValue.ShouldBeTrue();
        await WaitUntil(() => task.IsCompleted);
        await task;
        device1.IsBusy.CurrentValue.ShouldBeFalse();

        // DispenseCash
        var counts = new[] { new CashDenominationCount(1000m, 1) };
        task = device1.DispenseCashAsync(counts);
        device1.IsBusy.CurrentValue.ShouldBeTrue();
        TimeProvider.Advance(TimeSpan.FromMilliseconds(150));
        await task;
        device1.IsBusy.CurrentValue.ShouldBeFalse();
    }

    /// <summary>在庫操作の委譲を確認します。</summary>
    [Fact]
    public async Task InventoryOperations_ShouldBeDelegated()
    {
        await device1.OpenAsync();
        await device1.ClaimAsync(0);
        await device1.EnableAsync();

        var bill = new DenominationKey(1000, CurrencyCashType.Bill);
        Inventory.SetCount(bill, 0); // Register key for FindKey
        var counts = new[] { new CashDenominationCount(1000m, 5) };
        
        await device1.AdjustInventoryAsync(counts);
        var inv = await device1.ReadInventoryAsync();
        inv.GetCount(bill).ShouldBe(5);

        await device1.PurgeCashAsync();
        inv = await device1.ReadInventoryAsync();
        inv.GetCount(bill).ShouldBe(0);
        StatusManager.State.GetExitPortCounts(ExitPort.Collection)[bill].ShouldBe(5);
    }

    /// <summary>診断操作の委譲を確認します。</summary>
    [Fact]
    public async Task CheckHealth_ShouldBeDelegated()
    {
        var report = await device1.CheckHealthAsync(HealthCheckLevel.Internal);
        report.ShouldContain("OK");
    }

    /// <summary>ガード節の多段階検証（ErrorCode の詳細確認）</summary>
    [Fact]
    public async Task Guards_ShouldThrowCorrectErrorCodes_ForAllMethods()
    {
        // 検証対象の全アクション
        var actions = new List<Func<ICashChangerDevice, Task>>
        {
            d => d.BeginDepositAsync(),
            d => d.FixDepositAsync(),
            d => d.PauseDepositAsync(DeviceDepositPause.Pause),
            d => d.RepayDepositAsync(),
            d => d.EndDepositAsync(DepositAction.NoChange),
            d => d.DispenseChangeAsync(1000),
            d => d.DispenseCashAsync(new[] { new CashDenominationCount(1000m, 1) }),
            d => d.AdjustInventoryAsync(new[] { new CashDenominationCount(1000m, 1) }),
            d => d.PurgeCashAsync(),
            d => d.DirectIOAsync(DirectIOCommands.TakeCash, (int)ExitPort.Normal, null!)
        };

        foreach (var action in actions)
        {
            // 1. Not Opened
            var ex = await Should.ThrowAsync<DeviceException>(() => action(device1));
            ex.ErrorCode.ShouldBe(DeviceErrorCode.Closed);
        }

        await device1.OpenAsync();

        foreach (var action in actions)
        {
            // 2. Not Claimed
            var ex = await Should.ThrowAsync<DeviceException>(() => action(device1));
            ex.ErrorCode.ShouldBe(DeviceErrorCode.Illegal);
        }

        await device1.ClaimAsync(0);

        foreach (var action in actions)
        {
            // 3. Not Enabled
            var ex = await Should.ThrowAsync<DeviceException>(() => action(device1));
            ex.ErrorCode.ShouldBe(DeviceErrorCode.Disabled);
        }

        // All OK after Enable
        await device1.EnableAsync();
        // (ここでは最初の一個だけ確認。全体は各個別テストで確認済み)
        await Should.NotThrowAsync(() => device1.BeginDepositAsync());
    }

    /// <summary>FindKey で未定義の金種を指定した場合の例外を確認します。</summary>
    [Fact]
    public async Task FindKey_ShouldThrow_WhenDenominationNotFound()
    {
        await device1.OpenAsync();
        await device1.ClaimAsync(0);
        await device1.EnableAsync();

        // Already has bill1000? No, new device.
        // But invalid counts should still fail because it's not registered.
        var invalidCounts = new[] { new CashDenominationCount(12345m, 1) };
        await Should.ThrowAsync<DeviceException>(() => device1.AdjustInventoryAsync(invalidCounts));
    }
}
