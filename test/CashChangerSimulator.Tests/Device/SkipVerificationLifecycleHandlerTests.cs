using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device.Coordination;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PointOfService;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>検証スキップ版ライフサイクルハンドラの状態遷移およびライフサイクル操作を検証するテストクラス。</summary>
public class SkipVerificationLifecycleHandlerTests
{
    private readonly HardwareStatusManager _hardware;
    private readonly Mock<IUposMediator> _mediator;
    private readonly TransactionHistory _history;
    private readonly SkipVerificationLifecycleHandler _handler;

    public SkipVerificationLifecycleHandlerTests()
    {
        _hardware = new HardwareStatusManager();
        _mediator = new Mock<IUposMediator>();
        _history = new TransactionHistory();
        _handler = new SkipVerificationLifecycleHandler(_hardware, _mediator.Object, _history, NullLogger.Instance);
    }

    /// <summary>ハンドラの状態がハードウェア接続およびメディエータのビジー状態を反映することを検証します。</summary>
    [Fact]
    public void State_ShouldReflectHardwareAndMediator()
    {
        // Closed
        _hardware.SetConnected(false);
        _handler.State.ShouldBe(ControlState.Closed);

        // Busy
        _hardware.SetConnected(true);
        _mediator.Setup(m => m.IsBusy).Returns(true);
        _handler.State.ShouldBe(ControlState.Busy);

        // Idle
        _mediator.Setup(m => m.IsBusy).Returns(false);
        _handler.State.ShouldBe(ControlState.Idle);
    }

    /// <summary>Open, Claim, Close の各ライフサイクル操作がハードウェアおよび履歴に正しく反映されることを検証します。</summary>
    [Fact]
    public void Lifecycle_ShouldWork()
    {
        // Open
        _handler.Open(() => { });
        _hardware.IsConnected.Value.ShouldBeTrue();
        _history.Entries.ShouldContain(e => e.Type == TransactionType.Open);

        // Claim
        _handler.Claim(0, _ => { });
        _mediator.VerifySet(m => m.Claimed = true);
        _history.Entries.ShouldContain(e => e.Type == TransactionType.Claim);

        // Mediator Claimed mock for Close/Release tests
        _mediator.Setup(m => m.Claimed).Returns(true);

        // Close
        _handler.Close(() => { });
        _hardware.IsConnected.Value.ShouldBeFalse();
        _history.Entries.ShouldContain(e => e.Type == TransactionType.Release);
        _history.Entries.ShouldContain(e => e.Type == TransactionType.Close);
    }

    /// <summary>Closed 状態で Claim や Release を試みた際に例外が発生することを検証します。</summary>
    [Fact]
    public void ClaimAndRelease_ShouldThrowWhenClosed()
    {
        _hardware.SetConnected(false);
        
        Should.Throw<PosControlException>(() => _handler.Claim(0, _ => { }))
            .ErrorCode.ShouldBe(ErrorCode.Closed);
            
        Should.Throw<PosControlException>(() => _handler.Release(() => { }))
            .ErrorCode.ShouldBe(ErrorCode.Closed);
    }

    /// <summary>Open 状態で Release を実行した際の正常動作を検証します。</summary>
    [Fact]
    public void Release_ShouldWorkWhenOpen()
    {
        _hardware.SetConnected(true);
        _handler.Release(() => { });
        
        _mediator.VerifySet(m => m.Claimed = false);
        _history.Entries.ShouldContain(e => e.Type == TransactionType.Release);
    }

    /// <summary>DeviceEnabled などのプロパティがメディエータへ正しく委譲されることを検証します。</summary>
    [Fact]
    public void Properties_ShouldProxyToMediator()
    {
        _mediator.SetupProperty(m => m.DeviceEnabled);
        _mediator.SetupProperty(m => m.DataEventEnabled);

        _handler.DeviceEnabled = true;
        _mediator.Object.DeviceEnabled.ShouldBeTrue();
        _handler.DeviceEnabled.ShouldBeTrue();

        _handler.DataEventEnabled = true;
        _mediator.Object.DataEventEnabled.ShouldBeTrue();
        _handler.DataEventEnabled.ShouldBeTrue();
    }
}
