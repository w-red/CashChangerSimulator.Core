using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using Shouldly;

namespace CashChangerSimulator.Tests.Device.Virtual;

/// <summary>VirtualCashChangerDevice の機�E検証チE�E��E�ト</summary>
[Collection("SequentialHardwareTests")]
public class VirtualCashChangerDeviceTests : DeviceTestBase
{
    private readonly ICashChangerDevice device1;
    private readonly ICashChangerDevice device2;

    /// <summary>チE�E��E�ト用のインスタンスを�E期化します</summary>
    public VirtualCashChangerDeviceTests()
    {
        // 吁E�E��E�ストで共有�E Mutex 名前空間を使用して競合を避ける
        var testMutexName = GenerateUniqueMutexName();
        device1 = CreateDevice(testMutexName);
        device2 = CreateDevice(testMutexName);
    }

    /// <summary>褁E�E��E�のインスタンスで同時に排他権(Claim)を取得しようとした場合に例外が発生することを確認します</summary>
    [Fact]
    public async Task ConcurrentClaimShouldThrowException()
    {
        // Arrange
        await device1.OpenAsync();
        await device2.OpenAsync();

        // Act & Assert
        await device1.ClaimAsync(TestTimingConstants.ShortDelayMs);

        // 別タスクで device2.Claim を実行し、例外を確認すめE
        var task = Task.Run(() => device2.ClaimAsync(TestTimingConstants.ShortDelayMs));

        // Assert: 別タスクからの Claim は失敗する�EぁE
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

        // Exception が投げられなぁE�E��E�とで成功を確誁E
    }

    /// <summary>チE�E��E�イスをオープンした際、接続状態が正しく更新されることを確認します</summary>
    [Fact]
    public async Task OpenShouldSetConnected()
    {
        await device1.OpenAsync();
        StatusManager.IsConnected.CurrentValue.ShouldBeTrue();
        device1.State.CurrentValue.ShouldBe(PosSharp.Abstractions.ControlState.Idle);
        }

        /// <summary>チE��イスをクローズした際、�E断状態およ�E無効状態になることを確認します</summary>
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

        /// <summary>排他権取得済みの状態でチE��イスを有効化できることを確認します</summary>
        [Fact]
        public async Task EnableShouldSucceedWhenClaimed()
        {
        await device1.OpenAsync();
        await device1.ClaimAsync(TestTimingConstants.ShortDelayMs);
        await device1.EnableAsync();
        StatusManager.DeviceEnabled.CurrentValue.ShouldBeTrue();
        device1.State.CurrentValue.ShouldBe(PosSharp.Abstractions.ControlState.Idle);
    }

    /// <summary>排他権を取得してぁE�E��E�ぁE�E��E�態でチE�E��E�イスを有効化しようとした場合に例外が発生することを確認します</summary>
    [Fact]
    public async Task EnableShouldThrowWhenNotClaimed()
    {
        await device1.OpenAsync();
        await Should.ThrowAsync<DeviceException>(async () => await device1.EnableAsync());
    }

    /// <summary>チE�E��E�イスが無効な状態で入金を開始しようとした場合に例外が発生することを確認します</summary>
    [Fact]
    public async Task DepositShouldThrowWhenNotEnabled()
    {
        await device1.OpenAsync();
        await device1.ClaimAsync(TestTimingConstants.ShortDelayMs);

        // Not enabled
        await Should.ThrowAsync<DeviceException>(async () => await device1.BeginDepositAsync());
    }

    /// <summary>チE�E��E�イスが無効な状態で出金を開始しようとした場合に例外が発生することを確認します</summary>
    [Fact]
    public async Task DispenseShouldThrowWhenNotEnabled()
    {
        await device1.OpenAsync();
        await device1.ClaimAsync(TestTimingConstants.ShortDelayMs);

        // Not enabled
        await Should.ThrowAsync<DeviceException>(async () => await device1.DispenseChangeAsync(1000));
    }

    /// <summary>在庫惁E�E��E�の読み取りが空でなぁE�E��E�ンベントリを返すことを確認します</summary>
    [Fact]
    public async Task ReadInventoryShouldReturnCorrectData()
    {
        await device1.OpenAsync();
        await device1.ClaimAsync(TestTimingConstants.ShortDelayMs);
        await device1.EnableAsync();

        // DepositController 経由で預け入れを行う(VirtualCashChangerDevice のメソチE�E��E�を使用)
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




