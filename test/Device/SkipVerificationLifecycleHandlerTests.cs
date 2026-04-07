using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device.PosForDotNet.Coordination;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PointOfService;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>検証スキップ版ライフサイクルハンドラの状態遷移およびライフサイクル操作を検証するテストクラス。</summary>
public class SkipVerificationLifecycleHandlerTests
{
    private readonly HardwareStatusManager hardware;
    private readonly Mock<IUposMediator> mediator;
    private readonly TransactionHistory history;
    private readonly SkipVerificationLifecycleHandler handler;

    public SkipVerificationLifecycleHandlerTests()
    {
        hardware = new HardwareStatusManager();
        mediator = new Mock<IUposMediator>();
        history = new TransactionHistory();
        handler = new SkipVerificationLifecycleHandler(hardware, mediator.Object, history, NullLogger.Instance);
    }

    /// <summary>ハンドラの状態がハードウェア接続およびメディエータのビジー状態を反映することを検証します。</summary>
    [Fact]
    public void StateShouldReflectHardwareAndMediator()
    {
        // Closed
        hardware.SetConnected(false);
        handler.State.ShouldBe(ControlState.Closed);

        // Busy
        hardware.SetConnected(true);
        mediator.Setup(m => m.IsBusy).Returns(true);
        handler.State.ShouldBe(ControlState.Busy);

        // Idle
        mediator.Setup(m => m.IsBusy).Returns(false);
        handler.State.ShouldBe(ControlState.Idle);
    }

    /// <summary>Open, Claim, Close の各ライフサイクル操作がハードウェアおよび履歴に正しく反映されることを検証します。</summary>
    [Fact]
    public void LifecycleShouldWork()
    {
        // Open
        handler.Open(() => { });
        hardware.IsConnected.Value.ShouldBeTrue();
        history.Entries.ShouldContain(e => e.Type == TransactionType.Open);

        // Claim
        handler.Claim(0, _ => { });
        mediator.VerifySet(m => m.Claimed = true);
        history.Entries.ShouldContain(e => e.Type == TransactionType.Claim);

        // Mediator Claimed mock for Close/Release tests
        mediator.Setup(m => m.Claimed).Returns(true);

        // Close
        handler.Close(() => { });
        hardware.IsConnected.Value.ShouldBeFalse();
        history.Entries.ShouldContain(e => e.Type == TransactionType.Release);
        history.Entries.ShouldContain(e => e.Type == TransactionType.Close);
    }

    /// <summary>Closed 状態で Claim や Release を試みた際に例外が発生することを検証します。</summary>
    [Fact]
    public void ClaimAndReleaseShouldThrowWhenClosed()
    {
        hardware.SetConnected(false);

        Should.Throw<PosControlException>(() => handler.Claim(0, _ => { }))
            .ErrorCode.ShouldBe(ErrorCode.Closed);

        Should.Throw<PosControlException>(() => handler.Release(() => { }))
            .ErrorCode.ShouldBe(ErrorCode.Closed);
    }

    /// <summary>Open 状態で Release を実行した際の正常動作を検証します。</summary>
    [Fact]
    public void ReleaseShouldWorkWhenOpen()
    {
        hardware.SetConnected(true);
        handler.Release(() => { });

        mediator.VerifySet(m => m.Claimed = false);
        history.Entries.ShouldContain(e => e.Type == TransactionType.Release);
    }

    /// <summary>DeviceEnabled などのプロパティがメディエータへ正しく委譲されることを検証します。</summary>
    [Fact]
    public void PropertiesShouldProxyToMediator()
    {
        mediator.SetupProperty(m => m.DeviceEnabled);
        mediator.SetupProperty(m => m.DataEventEnabled);

        handler.DeviceEnabled = true;
        mediator.Object.DeviceEnabled.ShouldBeTrue();
        handler.DeviceEnabled.ShouldBeTrue();

        handler.DataEventEnabled = true;
        mediator.Object.DataEventEnabled.ShouldBeTrue();
        handler.DataEventEnabled.ShouldBeTrue();
    }
}
