using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace CashChangerSimulator.Tests.Core;

/// <summary>HardwareStatusManager の各状態遷移、エラー設定、およびグローバルロック連携を検証するテストクラス。</summary>
public class HardwareStatusManagerTests
{
    private readonly HardwareStatusManager manager = new();

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

    /// <summary>GlobalLockManager が未設定の場合の RefreshClaimedStatus を検証する。</summary>
    [Fact]
    public void RefreshClaimedStatusWithoutLockManagerShouldReturnCurrentValue()
    {
        manager.SetClaimedByAnother(true);
        manager.RefreshClaimedStatus().ShouldBeTrue();
        manager.SetClaimedByAnother(false);
        manager.RefreshClaimedStatus().ShouldBeFalse();
    }

    /// <summary>GlobalLockManager 連携時の RefreshClaimedStatus とロック取得を検証する。</summary>
    [Fact]
    public void GlobalLockIntegrationShouldWork()
    {
        using var lockManager = new GlobalLockManager("TestLock_" + Guid.NewGuid(), NullLogger.Instance);
        manager.SetGlobalLockManager(lockManager);

        // 自インスタンスがロックを持っていない状態
        manager.RefreshClaimedStatus().ShouldBeFalse();

        // ロック取得試行
        manager.TryAcquireGlobalLock().ShouldBeTrue();
        manager.IsClaimedByAnother.Value.ShouldBeFalse();

        // ロック解放
        manager.ReleaseGlobalLock();
    }

    /// <summary>破棄（Dispose）後の状態設定が無視されることを検証する。</summary>
    [Fact]
    public void AfterDisposeOperationsShouldBeIgnored()
    {
        manager.Dispose();
        manager.IsDisposed.ShouldBeTrue();

        // 操作しても値が変わらないこと（または例外が出ないこと）を確認
        manager.SetConnected(true);
        manager.IsConnected.Value.ShouldBeFalse();

        manager.SetJammed(true);
        manager.IsJammed.Value.ShouldBeFalse();

        manager.SetDeviceError(100);
        manager.IsDeviceError.Value.ShouldBeFalse();

        manager.SetClaimedByAnother(true);
        manager.IsClaimedByAnother.Value.ShouldBeFalse();

        manager.RefreshClaimedStatus().ShouldBeFalse();

        // Double dispose
        manager.Dispose();
    }
}
