using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device.Coordination;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PointOfService;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>標準的なライフサイクルハンドラの状態遷移、Open/Claim/Close/Release 操作を検証するテストクラス。</summary>
public class StandardLifecycleHandlerTests
{
    private readonly HardwareStatusManager _hardware;
    private readonly Mock<IUposMediator> _mediator;
    private readonly TransactionHistory _history;
    private readonly StandardLifecycleHandler _handler;

    public StandardLifecycleHandlerTests()
    {
        _hardware = new HardwareStatusManager();
        _mediator = new Mock<IUposMediator>();
        _history = new TransactionHistory();
        _handler = new StandardLifecycleHandler(_hardware, _mediator.Object, _history, NullLogger.Instance);
    }

    /// <summary>ハンドラの状態がハードウェア接続およびメディエータのビジー状態を正しく反映することを検証します。</summary>
    [Fact]
    public void StateShouldReflectHardwareAndMediator()
    {
        // Closed
        _hardware.SetConnected(false);
        _handler.State.ShouldBe(ControlState.Closed);

        // Idle
        _hardware.SetConnected(true);
        _mediator.Setup(m => m.IsBusy).Returns(false);
        _handler.State.ShouldBe(ControlState.Idle);

        // Busy
        _mediator.Setup(m => m.IsBusy).Returns(true);
        _handler.State.ShouldBe(ControlState.Busy);
    }

    /// <summary>Closed 状態で DeviceEnabled を設定しようとした際に例外が発生することを検証します。</summary>
    [Fact]
    public void DeviceEnabledSetterShouldThrowWhenClosed()
    {
        _hardware.SetConnected(false);
        Should.Throw<PosControlException>(() => _handler.DeviceEnabled = true)
            .ErrorCode.ShouldBe(ErrorCode.Closed);
    }

    /// <summary>Unclaimed 状態で DeviceEnabled を設定しようとした際に例外が発生することを検証します。</summary>
    [Fact]
    public void DeviceEnabledSetterShouldThrowWhenNotClaimed()
    {
        _hardware.SetConnected(true);
        _mediator.Setup(m => m.Claimed).Returns(false);
        Should.Throw<PosControlException>(() => _handler.DeviceEnabled = true)
            .ErrorCode.ShouldBe(ErrorCode.NotClaimed);
    }

    /// <summary>他者に Claim されている状態で DeviceEnabled を設定しようとした際に例外が発生することを検証します。</summary>
    [Fact]
    public void DeviceEnabledSetterShouldThrowWhenClaimedByAnother()
    {
        _hardware.SetConnected(true);
        _hardware.SetClaimedByAnother(true);
        Should.Throw<PosControlException>(() => _handler.DeviceEnabled = true)
            .ErrorCode.ShouldBe(ErrorCode.Claimed);
    }

    /// <summary>Claim されている状態で DeviceEnabled が正常に設定できることを検証します。</summary>
    [Fact]
    public void DeviceEnabledSetterShouldWorkWhenClaimed()
    {
        _hardware.SetConnected(true);
        _mediator.Setup(m => m.Claimed).Returns(true);
        _mediator.SetupProperty(m => m.DeviceEnabled);

        _handler.DeviceEnabled = true;
        _mediator.Object.DeviceEnabled.ShouldBeTrue();
    }

    /// <summary>既に Open されている状態での Open 呼び出しが正常に処理されることを検証します。</summary>
    [Fact]
    public void OpenShouldHandleAlreadyOpen()
    {
        _hardware.SetConnected(true);
        var baseCalled = false;
        _handler.Open(() => baseCalled = true);
        
        baseCalled.ShouldBeFalse();
        _mediator.Verify(m => m.SetSuccess(), Times.Once);
    }

    /// <summary>Open 処理中の例外発生時に、ハードウェア接続状態が正しく更新されることを検証します。</summary>
    [Fact]
    public void OpenShouldHandleBaseException()
    {
        _hardware.SetConnected(false);
        _handler.Open(() => throw new Exception("Test"));
        
        _hardware.IsConnected.Value.ShouldBeTrue();
        _history.Entries.ShouldContain(e => e.Type == TransactionType.Open);
    }

    /// <summary>既に Closed 状態での Close 呼び出しが正常に処理されることを検証します。</summary>
    [Fact]
    public void CloseShouldHandleAlreadyClosed()
    {
        _hardware.SetConnected(false);
        var baseCalled = false;
        _handler.Close(() => baseCalled = true);
        
        baseCalled.ShouldBeFalse();
        _mediator.Verify(m => m.SetSuccess(), Times.Once);
    }

    /// <summary>Close 処理中の例外発生時に、暗黙的な Release と Close が行われることを検証します。</summary>
    [Fact]
    public void CloseShouldHandleBaseExceptionAndImplicitRelease()
    {
        _hardware.SetConnected(true);
        _mediator.Setup(m => m.Claimed).Returns(true);
        
        _handler.Close(() => throw new Exception("Test"));
        
        _hardware.IsConnected.Value.ShouldBeFalse();
        _history.Entries.ShouldContain(e => e.Type == TransactionType.Release);
        _history.Entries.ShouldContain(e => e.Type == TransactionType.Close);
    }

    /// <summary>Closed 状態で Claim を試みた際に例外が発生することを検証します。</summary>
    [Fact]
    public void ClaimShouldThrowWhenClosed()
    {
        _hardware.SetConnected(false);
        Should.Throw<PosControlException>(() => _handler.Claim(0, _ => { }))
            .ErrorCode.ShouldBe(ErrorCode.Closed);
    }

    /// <summary>既に Claim されている状態での Claim 呼び出しが正常に処理されることを検証します。</summary>
    [Fact]
    public void ClaimShouldHandleAlreadyClaimed()
    {
        _hardware.SetConnected(true);
        _mediator.Setup(m => m.Claimed).Returns(true);
        
        var baseCalled = false;
        _handler.Claim(0, _ => baseCalled = true);
        
        baseCalled.ShouldBeFalse();
    }

    /// <summary>Claim 処理中の例外発生時に、メディエータの状態が正しく更新されることを検証します。</summary>
    [Fact]
    public void ClaimShouldHandleBaseException()
    {
        _hardware.SetConnected(true);
        _mediator.Setup(m => m.Claimed).Returns(false);
        
        _handler.Claim(0, _ => throw new Exception("Test"));
        
        _mediator.VerifySet(m => m.Claimed = true);
        _history.Entries.ShouldContain(e => e.Type == TransactionType.Claim);
    }

    /// <summary>Closed 状態で Release を試みた際に例外が発生することを検証します。</summary>
    [Fact]
    public void ReleaseShouldThrowWhenClosed()
    {
        _hardware.SetConnected(false);
        Should.Throw<PosControlException>(() => _handler.Release(() => { }))
            .ErrorCode.ShouldBe(ErrorCode.Closed);
    }

    /// <summary>Claim されていない状態での Release 呼び出しが正常に処理されることを検証します。</summary>
    [Fact]
    public void ReleaseShouldHandleNotClaimed()
    {
        _hardware.SetConnected(true);
        _mediator.Setup(m => m.Claimed).Returns(false);
        
        var baseCalled = false;
        _handler.Release(() => baseCalled = true);
        
        baseCalled.ShouldBeFalse();
    }

    /// <summary>Release 処理中の例外発生時に、メディエータの状態が正しく更新されることを検証します。</summary>
    [Fact]
    public void ReleaseShouldHandleBaseException()
    {
        _hardware.SetConnected(true);
        _mediator.Setup(m => m.Claimed).Returns(true);
        
        _handler.Release(() => throw new Exception("Test"));
        
        _mediator.VerifySet(m => m.Claimed = false);
        _history.Entries.ShouldContain(e => e.Type == TransactionType.Release);
    }
}
