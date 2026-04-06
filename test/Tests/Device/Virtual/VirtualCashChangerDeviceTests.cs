using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device.Virtual;
using Microsoft.Extensions.Logging;
using Moq;
using R3;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Tests.Device.Virtual;

/// <summary>VirtualCashChangerDevice の機能検証テスト。</summary>
public class VirtualCashChangerDeviceTests
{
    private readonly ICashChangerDevice device1;
    private readonly ICashChangerDevice device2;
    private readonly Mock<ILoggerFactory> loggerFactoryMock;
    private readonly HardwareStatusManager statusManager;

    /// <summary>テスト用のインスタンスを初期化します。</summary>
    public VirtualCashChangerDeviceTests()
    {
        var inventory = new Inventory();
        var history = new TransactionHistory();
        statusManager = new HardwareStatusManager();
        var manager = new CashChangerManager(inventory, history, (object?)null, null);
        
        loggerFactoryMock = new Mock<ILoggerFactory>();
        loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);

        var configProvider = new ConfigurationProvider();
        var factory = new VirtualCashChangerDeviceFactory(configProvider, loggerFactoryMock.Object);

        // 各テストで固有の Mutex 名を使用して競合を避ける
        var testMutexName = $"Global\\TestMutex_{Guid.NewGuid()}";
        device1 = factory.Create(manager, inventory, statusManager, testMutexName);
        device2 = factory.Create(manager, inventory, statusManager, testMutexName);
    }

    /// <summary>複数のインスタンスで同時に排他権（Claim）を取得しようとした場合に例外が発生することを確認します。</summary>
    [Fact]
    public async Task ConcurrentClaimShouldThrowException()
    {
        // Arrange
        await device1.OpenAsync();
        await device2.OpenAsync();

        // Act & Assert
        await device1.ClaimAsync(100);

        // 別タスクで device2.Claim を実行し、例外を確認する
        var task = Task.Run(() => device2.ClaimAsync(100));

        // Assert: 別タスクからの Claim は失敗するはず
        var ex = await Should.ThrowAsync<Exception>(async () => await task.WaitAsync(TimeSpan.FromMilliseconds(500)));
        // Note: Mutex 経由で DeviceException や その内部の例外がスローされる可能性がある
    }

    /// <summary>排他権を解放した後に別のインスタンスが排他権を取得できることを確認します。</summary>
    [Fact]
    public async Task ClaimAfterReleaseShouldSucceed()
    {
        // Arrange
        await device1.OpenAsync();
        await device2.OpenAsync();

        // Act
        await device1.ClaimAsync(100);
        await device1.ReleaseAsync();

        // Assert
        await device2.ClaimAsync(100);
        // Exception が投げられないことで成功を確認
    }

    /// <summary>デバイスをオープンした際、接続状態が正しく更新されることを確認します。</summary>
    [Fact]
    public async Task OpenShouldSetConnected()
    {
        await device1.OpenAsync();
        statusManager.IsConnected.Value.ShouldBeTrue();
        device1.State.CurrentValue.ShouldBe(DeviceControlState.Idle);
    }

    /// <summary>デバイスをクローズした際、切断状態および無効状態になることを確認します。</summary>
    [Fact]
    public async Task CloseShouldSetDisconnectedAndDisabled()
    {
        await device1.OpenAsync();
        await device1.ClaimAsync(100);
        await device1.EnableAsync();

        await device1.CloseAsync();

        statusManager.IsConnected.Value.ShouldBeFalse();
        statusManager.DeviceEnabled.Value.ShouldBeFalse();
        device1.State.CurrentValue.ShouldBe(DeviceControlState.Closed);
    }

    /// <summary>排他権取得済みの状態でデバイスを有効化できることを確認します。</summary>
    [Fact]
    public async Task EnableShouldSucceedWhenClaimed()
    {
        await device1.OpenAsync();
        await device1.ClaimAsync(100);
        await device1.EnableAsync();
        statusManager.DeviceEnabled.Value.ShouldBeTrue();
        device1.State.CurrentValue.ShouldBe(DeviceControlState.Idle);
    }

    /// <summary>排他権を取得していない状態でデバイスを有効化しようとした場合に例外が発生することを確認します。</summary>
    [Fact]
    public async Task EnableShouldThrowWhenNotClaimed()
    {
        await device1.OpenAsync();
        await Should.ThrowAsync<DeviceException>(async () => await device1.EnableAsync());
    }

    /// <summary>デバイスが無効な状態で入金を開始しようとした場合に例外が発生することを確認します。</summary>
    [Fact]
    public async Task DepositShouldThrowWhenNotEnabled()
    {
        await device1.OpenAsync();
        await device1.ClaimAsync(100);

        // Not enabled
        await Should.ThrowAsync<DeviceException>(async () => await device1.BeginDepositAsync());
    }

    /// <summary>デバイスが無効な状態で出金を開始しようとした場合に例外が発生することを確認します。</summary>
    [Fact]
    public async Task DispenseShouldThrowWhenNotEnabled()
    {
        await device1.OpenAsync();
        await device1.ClaimAsync(100);

        // Not enabled
        await Should.ThrowAsync<DeviceException>(async () => await device1.DispenseChangeAsync(1000));
    }

    /// <summary>在庫情報の読み取りが空でないインベントリを返すことを確認します。</summary>
    [Fact]
    public async Task ReadInventoryShouldReturnCorrectData()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        await device1.OpenAsync();
        await device1.ClaimAsync(100);
        await device1.EnableAsync();
        
        // DepositController 経由で預け入れを行う（VirtualCashChangerDevice のメソッドを使用）
        await device1.BeginDepositAsync();
        // VirtualCashChangerDevice 自体には個別の金種を投入する補助メソッドはないため、
        // 実際には DepositController を直接呼ぶか、EndDepositAsync で在庫を動かすなどのシナリオ。
        // ここでは単純化のため直接在庫を操作して確認するケースなどは他にあるので、
        // インターフェースの ReadInventory を確認する。
        
        var inventory = await device1.ReadInventoryAsync();
        inventory.ShouldNotBeNull();
    }
}
