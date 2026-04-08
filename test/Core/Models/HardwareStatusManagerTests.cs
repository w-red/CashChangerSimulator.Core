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
        manager.IsJammed.Value.ShouldBeFalse();
        manager.SetJammed(true, JamLocation.BillCassette1);
        manager.IsJammed.Value.ShouldBeTrue();
        manager.JamLocation.Value.ShouldBe(JamLocation.BillCassette1);

        manager.SetJammed(false);
        manager.IsJammed.Value.ShouldBeFalse();
        manager.JamLocation.Value.ShouldBe(JamLocation.None);
    }

    /// <summary>重なり（Overlap）状態を設定・取得できることを検証する。</summary>
    [Fact]
    public void SetOverlappedShouldUpdateState()
    {
        manager.IsOverlapped.Value.ShouldBeFalse();
        manager.SetOverlapped(true);
        manager.IsOverlapped.Value.ShouldBeTrue();
        manager.SetOverlapped(false);
        manager.IsOverlapped.Value.ShouldBeFalse();
    }

    /// <summary>接続状態を設定・取得できることを検証する。</summary>
    [Fact]
    public void SetConnectedShouldUpdateState()
    {
        manager.IsConnected.Value.ShouldBeFalse();
        manager.SetConnected(true);
        manager.IsConnected.Value.ShouldBeTrue();
        manager.SetConnected(false);
        manager.IsConnected.Value.ShouldBeFalse();
    }

    /// <summary>デバイスの有効化状態を設定・取得できることを検証する。</summary>
    [Fact]
    public void SetDeviceEnabledShouldUpdateState()
    {
        manager.DeviceEnabled.Value.ShouldBeFalse();
        manager.SetDeviceEnabled(true);
        manager.DeviceEnabled.Value.ShouldBeTrue();
        manager.SetDeviceEnabled(false);
        manager.DeviceEnabled.Value.ShouldBeFalse();
    }

    /// <summary>回収庫の取り外し状態を設定・取得できることを検証する。</summary>
    [Fact]
    public void SetCollectionBoxRemovedShouldUpdateState()
    {
        manager.IsCollectionBoxRemoved.Value.ShouldBeFalse();
        manager.SetCollectionBoxRemoved(true);
        manager.IsCollectionBoxRemoved.Value.ShouldBeTrue();
        manager.SetCollectionBoxRemoved(false);
        manager.IsCollectionBoxRemoved.Value.ShouldBeFalse();
    }

    /// <summary>他者による占有状態を直接設定・取得できることを検証する。</summary>
    [Fact]
    public void SetClaimedByAnotherShouldUpdateState()
    {
        manager.IsClaimedByAnother.Value.ShouldBeFalse();
        manager.SetClaimedByAnother(true);
        manager.IsClaimedByAnother.Value.ShouldBeTrue();
    }

    /// <summary>デバイスエラーとエラーコードを設定できることを検証する。</summary>
    [Fact]
    public void SetDeviceErrorShouldSetCodeAndStatus()
    {
        manager.IsDeviceError.Value.ShouldBeFalse();
        manager.SetDeviceError(111, 222);
        manager.IsDeviceError.Value.ShouldBeTrue();
        manager.CurrentErrorCode.Value.ShouldBe(111);
        manager.CurrentErrorCodeExtended.Value.ShouldBe(222);
    }

    /// <summary>ResetError がすべてのエラー状態を初期化することを検証する。</summary>
    [Fact]
    public void ResetErrorShouldClearAllStatus()
    {
        manager.SetJammed(true, JamLocation.BillCassette1);
        manager.SetOverlapped(true);
        manager.SetDeviceError(123);
        manager.SetCollectionBoxRemoved(true);

        manager.ResetError();

        manager.IsJammed.Value.ShouldBeFalse();
        manager.JamLocation.Value.ShouldBe(JamLocation.None);
        manager.IsOverlapped.Value.ShouldBeFalse();
        manager.IsDeviceError.Value.ShouldBeFalse();
        manager.IsCollectionBoxRemoved.Value.ShouldBeFalse();
        manager.CurrentErrorCode.Value.ShouldBeNull();
    }

    /// <summary>GlobalLockManager が未設定の場合の動作を検証する。</summary>
    [Fact]
    public void OperationsWithoutLockManagerShouldWork()
    {
        manager.SetClaimedByAnother(true);
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
        manager.IsClaimedByAnother.Value.ShouldBeFalse();

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
        using var d = manager.StatusUpdateEvents.Subscribe(_ => callCount++);

        // Act
        manager.SetConnected(true);
        manager.SetJammed(true);

        // Assert
        // Initial values are notified if they are reactive properties triggering on subscribe
        // R3 properties notify initial value on Subscribe by default? 
        // Actually, Observable.Merge of Selects will notify whenever any underlying property changes.
        callCount.ShouldBeGreaterThanOrEqualTo(2);
    }

    /// <summary>破棄（Dispose）後の状態設定が無視されることを検証する。</summary>
    [Fact]
    public void AfterDisposeOperationsShouldBeIgnored()
    {
        manager.Dispose();
        manager.IsDisposed.ShouldBeTrue();

        manager.SetConnected(true);
        manager.IsConnected.Value.ShouldBeFalse();

        manager.SetDeviceEnabled(true);
        manager.DeviceEnabled.Value.ShouldBeFalse();

        manager.SetJammed(true);
        manager.IsJammed.Value.ShouldBeFalse();

        manager.SetDeviceError(100);
        manager.IsDeviceError.Value.ShouldBeFalse();

        manager.SetClaimedByAnother(true);
        manager.IsClaimedByAnother.Value.ShouldBeFalse();

        manager.RefreshClaimedStatus().ShouldBeFalse();
        manager.SetGlobalLockManager(new GlobalLockManager("test", NullLogger.Instance)); // Should be ignored

        // Double dispose
        manager.Dispose();
    }
}
