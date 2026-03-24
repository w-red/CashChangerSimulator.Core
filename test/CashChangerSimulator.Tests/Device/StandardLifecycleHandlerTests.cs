using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device.Coordination;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PointOfService;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

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

    [Fact]
    public void State_ShouldReflectHardwareAndMediator()
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

    [Fact]
    public void DeviceEnabled_Setter_ShouldThrowWhenClosed()
    {
        _hardware.SetConnected(false);
        Should.Throw<PosControlException>(() => _handler.DeviceEnabled = true)
            .ErrorCode.ShouldBe(ErrorCode.Closed);
    }

    [Fact]
    public void DeviceEnabled_Setter_ShouldThrowWhenNotClaimed()
    {
        _hardware.SetConnected(true);
        _mediator.Setup(m => m.Claimed).Returns(false);
        Should.Throw<PosControlException>(() => _handler.DeviceEnabled = true)
            .ErrorCode.ShouldBe(ErrorCode.Illegal);
    }

    [Fact]
    public void DeviceEnabled_Setter_ShouldWorkWhenClaimed()
    {
        _hardware.SetConnected(true);
        _mediator.Setup(m => m.Claimed).Returns(true);
        _mediator.SetupProperty(m => m.DeviceEnabled);

        _handler.DeviceEnabled = true;
        _mediator.Object.DeviceEnabled.ShouldBeTrue();
    }

    [Fact]
    public void Open_ShouldHandleAlreadyOpen()
    {
        _hardware.SetConnected(true);
        var baseCalled = false;
        _handler.Open(() => baseCalled = true);
        
        baseCalled.ShouldBeFalse();
        _mediator.Verify(m => m.SetSuccess(), Times.Once);
    }

    [Fact]
    public void Open_ShouldHandleBaseException()
    {
        _hardware.SetConnected(false);
        _handler.Open(() => throw new Exception("Test"));
        
        _hardware.IsConnected.Value.ShouldBeTrue();
        _history.Entries.ShouldContain(e => e.Type == TransactionType.Open);
    }

    [Fact]
    public void Close_ShouldHandleAlreadyClosed()
    {
        _hardware.SetConnected(false);
        var baseCalled = false;
        _handler.Close(() => baseCalled = true);
        
        baseCalled.ShouldBeFalse();
        _mediator.Verify(m => m.SetSuccess(), Times.Once);
    }

    [Fact]
    public void Close_ShouldHandleBaseExceptionAndImplicitRelease()
    {
        _hardware.SetConnected(true);
        _mediator.Setup(m => m.Claimed).Returns(true);
        
        _handler.Close(() => throw new Exception("Test"));
        
        _hardware.IsConnected.Value.ShouldBeFalse();
        _history.Entries.ShouldContain(e => e.Type == TransactionType.Release);
        _history.Entries.ShouldContain(e => e.Type == TransactionType.Close);
    }

    [Fact]
    public void Claim_ShouldThrowWhenClosed()
    {
        _hardware.SetConnected(false);
        Should.Throw<PosControlException>(() => _handler.Claim(0, _ => { }))
            .ErrorCode.ShouldBe(ErrorCode.Closed);
    }

    [Fact]
    public void Claim_ShouldHandleAlreadyClaimed()
    {
        _hardware.SetConnected(true);
        _mediator.Setup(m => m.Claimed).Returns(true);
        
        var baseCalled = false;
        _handler.Claim(0, _ => baseCalled = true);
        
        baseCalled.ShouldBeFalse();
    }

    [Fact]
    public void Claim_ShouldHandleBaseException()
    {
        _hardware.SetConnected(true);
        _mediator.Setup(m => m.Claimed).Returns(false);
        
        _handler.Claim(0, _ => throw new Exception("Test"));
        
        _mediator.VerifySet(m => m.Claimed = true);
        _history.Entries.ShouldContain(e => e.Type == TransactionType.Claim);
    }

    [Fact]
    public void Release_ShouldThrowWhenClosed()
    {
        _hardware.SetConnected(false);
        Should.Throw<PosControlException>(() => _handler.Release(() => { }))
            .ErrorCode.ShouldBe(ErrorCode.Closed);
    }

    [Fact]
    public void Release_ShouldHandleNotClaimed()
    {
        _hardware.SetConnected(true);
        _mediator.Setup(m => m.Claimed).Returns(false);
        
        var baseCalled = false;
        _handler.Release(() => baseCalled = true);
        
        baseCalled.ShouldBeFalse();
    }

    [Fact]
    public void Release_ShouldHandleBaseException()
    {
        _hardware.SetConnected(true);
        _mediator.Setup(m => m.Claimed).Returns(true);
        
        _handler.Release(() => throw new Exception("Test"));
        
        _mediator.VerifySet(m => m.Claimed = false);
        _history.Entries.ShouldContain(e => e.Type == TransactionType.Release);
    }
}
