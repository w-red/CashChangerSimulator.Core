using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Coordination;
using Microsoft.PointOfService;
using Shouldly;
using System;
using System.IO;
using Xunit;

namespace CashChangerSimulator.Tests.Device;

/// <summary>UPOS エラーコード報告の優先順位（Precedence）を検証するテストクラス。</summary>
/// <remarks>
/// UPOS 1.15 仕様に基づき、以下の優先順位でエラーが報告されることを確認します。
/// 1. E_CLOSED
/// 2. E_CLAIMED
/// 3. E_NOTCLAIMED
/// 4. E_DISABLED
/// </remarks>
[Collection("GlobalLock")]
public class UposErrorPrecedenceTests
{
    private readonly string _lockPath;
    private readonly InternalSimulatorCashChanger _so;
    private readonly UposMediator _mediator;

    public UposErrorPrecedenceTests()
    {
        _lockPath = Path.Combine(AppContext.BaseDirectory, "LocalSettings", $"precedence_{Guid.NewGuid():N}.lock");
        _so = new InternalSimulatorCashChanger(new SimulatorDependencies(GlobalLockFilePath: _lockPath));
        _mediator = (UposMediator)_so.Context.Mediator;
        _mediator.SkipStateVerification = false;
    }

    [Fact]
    public void VerifyStateShouldPrioritizeClosedWhenMultipleErrorsExist()
    {
        // 状態: Closed, ClaimedByAnother: true, NotClaimed, Disabled
        _so.HardwareStatusManager.SetConnected(false); // Closed
        
        // 他のインスタンスで占有をシミュレート
        using var competitor = new InternalSimulatorCashChanger(new SimulatorDependencies(GlobalLockFilePath: _lockPath));
        competitor.SkipStateVerification = false;
        competitor.Open();
        competitor.Claim(0);

        // Mediator の Claimed, DeviceEnabled は初期値 false

        var ex = Should.Throw<PosControlException>(() => _mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: true));
        ex.ErrorCode.ShouldBe(ErrorCode.Closed);
    }

    [Fact]
    public void VerifyStateShouldPrioritizeClaimedWhenOpenButOccupiedByAnother()
    {
        // 状態: Open, ClaimedByAnother: true, NotClaimed, Disabled
        _so.Open();
        
        // 他のインスタンスで占有をシミュレート
        using var competitor = new InternalSimulatorCashChanger(new SimulatorDependencies(GlobalLockFilePath: _lockPath));
        competitor.SkipStateVerification = false;
        competitor.Open();
        competitor.Claim(0);

        // 自インスタンスでは Claim していない状態

        var ex = Should.Throw<PosControlException>(() => _mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: true));
        ex.ErrorCode.ShouldBe(ErrorCode.Claimed);
    }

    [Fact]
    public void VerifyStateShouldPrioritizeNotClaimedWhenOpenAndNotOccupiedButNotClaimedBySelf()
    {
        // 状態: Open, ClaimedByAnother: false, NotClaimed by self, Disabled
        _so.Open();
        _so.HardwareStatusManager.SetClaimedByAnother(false);

        var ex = Should.Throw<PosControlException>(() => _mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: true));
        ex.ErrorCode.ShouldBe(ErrorCode.NotClaimed);
    }

    [Fact]
    public void VerifyStateShouldReturnDisabledWhenOpenAndClaimedButNotEnabled()
    {
        // 状態: Open, ClaimedByAnother: false, Claimed by self, Disabled
        _so.Open();
        _so.Claim(0);
        _so.DeviceEnabled = false;

        var ex = Should.Throw<PosControlException>(() => _mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: true));
        ex.ErrorCode.ShouldBe(ErrorCode.Disabled);
    }

    [Fact]
    public void StandardLifecycleHandlerDeviceEnabledShouldUseCorrectPrecedence()
    {
        // DeviceEnabled プロパティへのセット時も優先順位が適用されることを確認
        
        // 1. Closed 優先
        _so.HardwareStatusManager.SetConnected(false);
        Should.Throw<PosControlException>(() => _so.DeviceEnabled = true)
            .ErrorCode.ShouldBe(ErrorCode.Closed);

        // 2. Claimed (Another) 優先
        _so.Open();
        
        // 他のインスタンスで占有をシミュレート
        using (var competitor = new InternalSimulatorCashChanger(new SimulatorDependencies(GlobalLockFilePath: _lockPath)))
        {
            competitor.SkipStateVerification = false;
            competitor.Open();
            competitor.Claim(0);
            
            Should.Throw<PosControlException>(() => _so.DeviceEnabled = true)
                .ErrorCode.ShouldBe(ErrorCode.Claimed);
        }

        // 3. NotClaimed (Self) 優先
        _so.HardwareStatusManager.SetClaimedByAnother(false);
        // まだ Claim していない
        Should.Throw<PosControlException>(() => _so.DeviceEnabled = true)
            .ErrorCode.ShouldBe(ErrorCode.NotClaimed);
    }

    [Fact]
    public void FileShareNoneShouldBlockOtherHandlesInSameProcess()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "LocalSettings", "test_internal.lock");
        var dir = Path.GetDirectoryName(path);
        if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
        if (File.Exists(path)) File.Delete(path);

        using (var streamA = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            // 同じプロセス内でも、FileShare.None で開かれたファイルは他から開けないはず
            Should.Throw<IOException>(() => 
                new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite)
            );
        }
    }
}
