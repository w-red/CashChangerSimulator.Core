using CashChangerSimulator.Device.PosForDotNet;
using CashChangerSimulator.Device.PosForDotNet.Coordination;
using CashChangerSimulator.Device.PosForDotNet.Models;
using Microsoft.PointOfService;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>UPOS エラーコード報告の優先順位（Precedence）を検証するテストクラス。.</summary>
/// <remarks>
/// UPOS 1.15 仕様に基づき、以下の優先順位でエラーが報告されることを確認します。
/// 1. E_CLOSED
/// 2. E_CLAIMED
/// 3. E_NOTCLAIMED
/// 4. E_DISABLED.
/// </remarks>
[Collection("GlobalLock")]
public class UposErrorPrecedenceTests
{
    private readonly string lockPath;
    private readonly InternalSimulatorCashChanger so;
    private readonly UposMediator mediator;

    public UposErrorPrecedenceTests()
    {
        lockPath = Path.Combine(AppContext.BaseDirectory, "LocalSettings", $"precedence_{Guid.NewGuid():N}.lock");
        so = new InternalSimulatorCashChanger(new SimulatorDependencies(GlobalLockFilePath: lockPath));
        mediator = (UposMediator)so.Context.Mediator;
        so.SkipStateVerification = false; // [FIX] Use facade property to trigger LifecycleHandler refresh
    }

    [Fact]
    public void VerifyStateShouldPrioritizeClosedWhenMultipleErrorsExist()
    {
        // 状態: Closed, ClaimedByAnother: true, NotClaimed, Disabled
        so.HardwareStatus.SetConnected(false); // Closed

        // 他のインスタンスで占有をシミュレート
        using var competitor = new InternalSimulatorCashChanger(new SimulatorDependencies(GlobalLockFilePath: lockPath));
        competitor.SkipStateVerification = false;
        competitor.Open();
        competitor.Claim(0);

        // Mediator の Claimed, DeviceEnabled は初期値 false
        var ex = Should.Throw<PosControlException>(() => mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: true));
        ex.ErrorCode.ShouldBe(ErrorCode.Closed);
    }

    [Fact]
    public void VerifyStateShouldPrioritizeClaimedWhenOpenButOccupiedByAnother()
    {
        // 状態: Open, ClaimedByAnother: true, NotClaimed, Disabled
        so.Open();

        // 他のインスタンスで占有をシミュレート
        using var competitor = new InternalSimulatorCashChanger(new SimulatorDependencies(GlobalLockFilePath: lockPath));
        competitor.SkipStateVerification = false;
        competitor.Open();
        competitor.Claim(0);

        // 自インスタンスでは Claim していない状態
        var ex = Should.Throw<PosControlException>(() => mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: true));
        ex.ErrorCode.ShouldBe(ErrorCode.Claimed);
    }

    [Fact]
    public void VerifyStateShouldPrioritizeNotClaimedWhenOpenAndNotOccupiedButNotClaimedBySelf()
    {
        // 状態: Open, ClaimedByAnother: false, NotClaimed by self, Disabled
        so.Open();
        so.HardwareStatus.SetClaimedByAnother(false);

        var ex = Should.Throw<PosControlException>(() => mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: true));
        ex.ErrorCode.ShouldBe(ErrorCode.NotClaimed);
    }

    [Fact]
    public void VerifyStateShouldReturnDisabledWhenOpenAndClaimedButNotEnabled()
    {
        // 状態: Open, ClaimedByAnother: false, Claimed by self, Disabled
        so.Open();
        so.Claim(0);
        so.DeviceEnabled = false;

        var ex = Should.Throw<PosControlException>(() => mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: true));
        ex.ErrorCode.ShouldBe(ErrorCode.Disabled);
    }

    [Fact]
    public async Task StandardLifecycleHandlerDeviceEnabledShouldUseCorrectPrecedence()
    {
        // DeviceEnabled プロパティへのセット時も優先順位が適用されることを確認

        // 1. Closed 優先
        so.HardwareStatus.SetConnected(false);
        Should.Throw<PosControlException>(() => so.DeviceEnabled = true)
            .ErrorCode.ShouldBe(ErrorCode.Closed);

        // 2. Claimed (Another) 優先
        so.Open();

        // 他のインスタンスで占有をシミュレート
        using (var competitor = new InternalSimulatorCashChanger(new SimulatorDependencies(GlobalLockFilePath: lockPath)))
        {
            competitor.SkipStateVerification = false;
            competitor.Open();
            competitor.Claim(0);

            Should.Throw<PosControlException>(() => so.DeviceEnabled = true)
                .ErrorCode.ShouldBe(ErrorCode.Claimed);
        }

        so.HardwareStatus.SetClaimedByAnother(false);

        // OSがファイルハンドルを閉じるのを待つためのわずかなウェイト
        await Task.Delay(100, TestContext.Current.CancellationToken).ConfigureAwait(false);

        // まだ Claim していない
        Should.Throw<PosControlException>(() => so.DeviceEnabled = true)
            .ErrorCode.ShouldBe(ErrorCode.NotClaimed);
    }

    [Fact]
    public void FileShareNoneShouldBlockOtherHandlesInSameProcess()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "LocalSettings", "test_internal.lock");
        var dir = Path.GetDirectoryName(path);
        if (dir != null && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        using (var streamA = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            // 同じプロセス内でも、FileShare.None で開かれたファイルは他から開けないはず
            Should.Throw<IOException>(() =>
                new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite));
        }
    }
}
