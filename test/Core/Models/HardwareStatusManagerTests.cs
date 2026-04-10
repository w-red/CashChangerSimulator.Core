using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using R3;
using Shouldly;

namespace CashChangerSimulator.Tests.Core.Models;

/// <summary>HardwareStatusManager の各状態遷移、エラー設定、およびグローバルロック連携を検証するテストクラス。</summary>
public class HardwareStatusManagerTests
{
    private readonly HardwareStatusManager manager = HardwareStatusManager.Create();

    /// <summary>ジャム状態と発生箇所を正しく設定・解除できることを検証する。</summary>
    [Fact]
    public void SetJammedShouldUpdateState()
    {
        manager.State.IsJammed.CurrentValue.ShouldBeFalse();
        
        manager.Input.IsJammed.Value = true;
        manager.Input.CurrentJamLocation.Value = JamLocation.BillCassette1;
        
        manager.State.IsJammed.CurrentValue.ShouldBeTrue();
        manager.State.CurrentJamLocation.CurrentValue.ShouldBe(JamLocation.BillCassette1);

        manager.Input.IsJammed.Value = false;
        manager.State.IsJammed.CurrentValue.ShouldBeFalse();
        manager.State.CurrentJamLocation.CurrentValue.ShouldBe(JamLocation.None); // パイプラインにより連動
    }

    /// <summary>重なり（Overlap）状態を設定・取得できることを検証する。</summary>
    [Fact]
    public void SetOverlappedShouldUpdateState()
    {
        manager.State.IsOverlapped.CurrentValue.ShouldBeFalse();
        manager.Input.IsOverlapped.Value = true;
        manager.State.IsOverlapped.CurrentValue.ShouldBeTrue();
        manager.Input.IsOverlapped.Value = false;
        manager.State.IsOverlapped.CurrentValue.ShouldBeFalse();
    }

    /// <summary>接続状態を設定・取得できることを検証する。</summary>
    [Fact]
    public void SetConnectedShouldUpdateState()
    {
        manager.State.IsConnected.CurrentValue.ShouldBeFalse();
        manager.Input.IsConnected.Value = true;
        manager.State.IsConnected.CurrentValue.ShouldBeTrue();
        manager.Input.IsConnected.Value = false;
        manager.State.IsConnected.CurrentValue.ShouldBeFalse();
    }

    /// <summary>デバイスの有効化状態を設定・取得できることを検証する。</summary>
    [Fact]
    public void SetDeviceEnabledShouldUpdateState()
    {
        manager.State.DeviceEnabled.CurrentValue.ShouldBeFalse();
        manager.Input.DeviceEnabled.Value = true;
        manager.State.DeviceEnabled.CurrentValue.ShouldBeTrue();
        manager.Input.DeviceEnabled.Value = false;
        manager.State.DeviceEnabled.CurrentValue.ShouldBeFalse();
    }

    /// <summary>回収庫の取り外し状態を設定・取得できることを検証する。</summary>
    [Fact]
    public void SetCollectionBoxRemovedShouldUpdateState()
    {
        manager.State.IsCollectionBoxRemoved.CurrentValue.ShouldBeFalse();
        manager.Input.IsCollectionBoxRemoved.Value = true;
        manager.State.IsCollectionBoxRemoved.CurrentValue.ShouldBeTrue();
        manager.Input.IsCollectionBoxRemoved.Value = false;
        manager.State.IsCollectionBoxRemoved.CurrentValue.ShouldBeFalse();
    }

    /// <summary>他者による占有状態を直接設定・取得できることを検証する。</summary>
    [Fact]
    public void SetClaimedByAnotherShouldUpdateState()
    {
        manager.State.IsClaimedByAnother.CurrentValue.ShouldBeFalse();
        manager.Input.IsClaimedByAnother.Value = true;
        manager.State.IsClaimedByAnother.CurrentValue.ShouldBeTrue();
    }

    /// <summary>デバイスエラーとエラーコードを設定できることを検証する。</summary>
    [Fact]
    public void SetDeviceErrorShouldSetCodeAndStatus()
    {
        manager.State.IsDeviceError.CurrentValue.ShouldBeFalse();
        
        manager.Input.CurrentErrorCode.Value = 111;
        manager.Input.CurrentErrorCodeExtended.Value = 222;
        manager.Input.IsDeviceError.Value = true;
        
        manager.State.IsDeviceError.CurrentValue.ShouldBeTrue();
        manager.State.CurrentErrorCode.CurrentValue.ShouldBe(111);
        manager.State.CurrentErrorCodeExtended.CurrentValue.ShouldBe(222);
    }

    /// <summary>ResetTrigger がすべてのエラー状態を初期化することを検証する。</summary>
    [Fact]
    public void ResetTriggerShouldClearAllStatus()
    {
        manager.Input.IsJammed.Value = true;
        manager.Input.CurrentJamLocation.Value = JamLocation.BillCassette1;
        manager.Input.IsOverlapped.Value = true;
        manager.Input.IsDeviceError.Value = true;
        manager.Input.IsCollectionBoxRemoved.Value = true;

        manager.Input.ResetTrigger.OnNext(Unit.Default);

        manager.State.IsJammed.CurrentValue.ShouldBeFalse();
        manager.State.CurrentJamLocation.CurrentValue.ShouldBe(JamLocation.None);
        manager.State.IsOverlapped.CurrentValue.ShouldBeFalse();
        manager.State.IsDeviceError.CurrentValue.ShouldBeFalse();
        manager.State.IsCollectionBoxRemoved.CurrentValue.ShouldBeFalse();
        manager.State.CurrentErrorCode.CurrentValue.ShouldBeNull();
    }

    /// <summary>GlobalLockManager が未設定の場合の動作を検証する。</summary>
    [Fact]
    public void OperationsWithoutLockManagerShouldWork()
    {
        manager.Input.IsClaimedByAnother.Value = true;
        manager.RefreshClaimedStatus().ShouldBeTrue();
        manager.TryAcquireGlobalLock().ShouldBeTrue();
        manager.ReleaseGlobalLock(); // No throw
    }

    /// <summary>GlobalLockManager 連携時の成否を検証する。</summary>
    [Fact]
    public void GlobalLockIntegrationShouldWork()
    {
        var lockName = "TestLock_" + Guid.NewGuid();
        using var lockManager = new GlobalLockManager(lockName, NullLogger.Instance);
        manager.SetGlobalLockManager(lockManager);

        // 自インスタンスがロックを持っていない状態
        manager.RefreshClaimedStatus().ShouldBeFalse();

        // ロック取得試行
        manager.TryAcquireGlobalLock().ShouldBeTrue();
        manager.State.IsClaimedByAnother.CurrentValue.ShouldBeFalse();

        // 別マネージャーでロックを奪い合う
        using var anotherLock = new GlobalLockManager(lockName, NullLogger.Instance);
        anotherLock.TryAcquire().ShouldBeFalse(); // Already held by lockManager

        // ロック解放
        manager.ReleaseGlobalLock();
        anotherLock.TryAcquire().ShouldBeTrue();

        manager.RefreshClaimedStatus().ShouldBeTrue(); // Now held by anotherLock
    }

    /// <summary>ステータス変更イベントが正しく通知されることを検証します。</summary>
    [Fact]
    public void StatusUpdateEventsShouldNotify()
    {
        // Arrange
        int callCount = 0;
        using var d = manager.State.StatusUpdateEvents.Subscribe(_ => callCount++);

        // Act
        manager.Input.IsConnected.Value = true;
        manager.Input.IsJammed.Value = true;

        // Assert
        callCount.ShouldBeGreaterThanOrEqualTo(2);
    }

    /// <summary>破棄（Dispose）後の状態設定で例外が発生することを検証する（R3 の標準挙動）。</summary>
    [Fact]
    public void AfterDisposeOperationsShouldThrow()
    {
        manager.Dispose();
        manager.IsDisposed.ShouldBeTrue();

        // R3 の ReactiveProperty は Dispose 後に Value 操作すると ObjectDisposedException を投げる
        Should.Throw<ObjectDisposedException>(() => manager.Input.IsConnected.Value = true);
        Should.Throw<ObjectDisposedException>(() => manager.Input.IsJammed.Value = true);
        
        // Double dispose
        manager.Dispose();
    }
}
