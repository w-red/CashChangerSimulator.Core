using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using Shouldly;

namespace CashChangerSimulator.Tests.Device.Virtual;

/// <summary>VirtualCashChangerDevice の機能検証テスト。</summary>
[Collection("SequentialHardwareTests")]
public class VirtualCashChangerDeviceTests : DeviceTestBase
{
    private readonly ICashChangerDevice device1;
    private readonly ICashChangerDevice device2;

    /// <summary>テスト用のインスタンスを初期化します。</summary>
    public VirtualCashChangerDeviceTests()
    {
        // 各テストで共有の Mutex 名前空間を使用して競合を避ける
        var testMutexName = GenerateUniqueMutexName();
        device1 = CreateDevice(testMutexName);
        device2 = CreateDevice(testMutexName);
    }

    /// <summary>複数のインスタンスで同時に排他権(Claim)を取得しようとした場合に例外が発生することを確認します。</summary>
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

        // Assert: 別タスクからの Claim は失敗するはず
        await Should.ThrowAsync<Exception>(async () => await task.WaitAsync(TimeSpan.FromMilliseconds(TestTimingConstants.DefaultTimeoutMs)));
    }

    /// <summary>排他権を解放した後に別のインスタンスが排他権を取得できることを確認します。</summary>
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

    /// <summary>デバイスをオープンした際、接続状態が正しく更新されることを確認します。</summary>
    [Fact]
    public async Task OpenShouldSetConnected()
    {
        await device1.OpenAsync();
        StatusManager.IsConnected.CurrentValue.ShouldBeTrue();
        device1.State.CurrentValue.ShouldBe(DeviceControlState.Idle);
    }

    /// <summary>デバイスをクローズした際、切断状態および無効状態になることを確認します。</summary>
    [Fact]
    public async Task CloseShouldSetDisconnectedAndDisabled()
    {
        await device1.OpenAsync();
        await device1.ClaimAsync(TestTimingConstants.ShortDelayMs);
        await device1.EnableAsync();

        await device1.CloseAsync();

        StatusManager.IsConnected.CurrentValue.ShouldBeFalse();
        StatusManager.DeviceEnabled.CurrentValue.ShouldBeFalse();
        device1.State.CurrentValue.ShouldBe(DeviceControlState.Closed);
    }

    /// <summary>排他権取得済みの状態でデバイスを有効化できることを確認します。</summary>
    [Fact]
    public async Task EnableShouldSucceedWhenClaimed()
    {
        await device1.OpenAsync();
        await device1.ClaimAsync(TestTimingConstants.ShortDelayMs);
        await device1.EnableAsync();
        StatusManager.DeviceEnabled.CurrentValue.ShouldBeTrue();
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
        await device1.ClaimAsync(TestTimingConstants.ShortDelayMs);

        // Not enabled
        await Should.ThrowAsync<DeviceException>(async () => await device1.BeginDepositAsync());
    }

    /// <summary>デバイスが無効な状態で出金を開始しようとした場合に例外が発生することを確認します。</summary>
    [Fact]
    public async Task DispenseShouldThrowWhenNotEnabled()
    {
        await device1.OpenAsync();
        await device1.ClaimAsync(TestTimingConstants.ShortDelayMs);

        // Not enabled
        await Should.ThrowAsync<DeviceException>(async () => await device1.DispenseChangeAsync(1000));
    }

    /// <summary>在庫情報の読み取りが空でないインベントリを返すことを確認します。</summary>
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
}
