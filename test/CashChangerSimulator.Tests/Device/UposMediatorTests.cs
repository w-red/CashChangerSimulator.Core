using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Coordination;
using Microsoft.PointOfService;
using CashChangerSimulator.Core.Opos;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Tests.Device;

public class UposMediatorTests
{
    private readonly InternalSimulatorCashChanger _so;
    private readonly UposMediator _mediator;

    public UposMediatorTests()
    {
        _so = new InternalSimulatorCashChanger();
        _mediator = new UposMediator(_so);
    }

    [Fact]
    public void VerifyState_ShouldThrowClosed_WhenClosed()
    {
        // Default state is Closed
        Should.Throw<PosControlException>(() => _mediator.VerifyState())
            .ErrorCode.ShouldBe(ErrorCode.Closed);
    }

    [Fact]
    public void VerifyState_ShouldThrowIllegal_WhenNotClaimed()
    {
        _so.Open();
        Should.Throw<PosControlException>(() => _mediator.VerifyState(mustBeClaimed: true))
            .ErrorCode.ShouldBe(ErrorCode.Illegal);
    }

    [Fact]
    public void VerifyState_ShouldThrowDisabled_WhenNotEnabled()
    {
        _so.Open();
        _so.Claim(0);
        Should.Throw<PosControlException>(() => _mediator.VerifyState(mustBeEnabled: true))
            .ErrorCode.ShouldBe(ErrorCode.Disabled);
    }

    [Fact]
    public void VerifyState_ShouldThrowBusy_WhenBusy()
    {
        _so.Open();
        _so.Claim(0);
        _so.DeviceEnabled = true;
        _mediator.IsBusy = true;
        
        Should.Throw<PosControlException>(() => _mediator.VerifyState(mustNotBeBusy: true))
            .ErrorCode.ShouldBe(ErrorCode.Busy);
    }

    [Fact]
    public void VerifyState_ShouldNotThrow_WhenAllConditionsMet()
    {
        _so.Open();
        _so.Claim(0);
        _so.DeviceEnabled = true;
        _mediator.IsBusy = false;

        _mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: true, mustNotBeBusy: true);
    }

    [Fact]
    public void VerifyState_ShouldSkip_WhenSkipFlagIsSet()
    {
        _mediator.SkipStateVerification = true;
        _mediator.VerifyState(); // Should not throw even if Closed
    }

    [Fact]
    public void ThrowIfBusy_ShouldThrow_WhenBusy()
    {
        Should.Throw<PosControlException>(() => UposMediator.ThrowIfBusy(true))
            .ErrorCode.ShouldBe(ErrorCode.Busy);
    }

    [Fact]
    public void ThrowIfDepositInProgress_ShouldThrow_WhenInProgress()
    {
        Should.Throw<PosControlException>(() => UposMediator.ThrowIfDepositInProgress(true))
            .ErrorCode.ShouldBe(ErrorCode.Illegal);
    }

    [Fact]
    public void HandleDispenseResult_ShouldSetCodesAndFireEvent_WhenAsync()
    {
        bool eventFired = false;
        _so.OnEventQueued = (e) => 
        {
            if (e is StatusUpdateEventArgs se && se.Status == (int)UposCashChangerStatusUpdateCode.AsyncFinished)
            {
                eventFired = true;
            }
        };

        _mediator.IsBusy = true;
        _mediator.HandleDispenseResult(ErrorCode.Extended, 123, true);

        _mediator.ResultCode.ShouldBe((int)ErrorCode.Extended);
        _mediator.ResultCodeExtended.ShouldBe(123);
        _mediator.AsyncResultCode.ShouldBe((int)ErrorCode.Extended);
        _mediator.AsyncResultCodeExtended.ShouldBe(123);
        _mediator.IsBusy.ShouldBeFalse();
        eventFired.ShouldBeTrue();
    }

    [Fact]
    public void Execute_ShouldHandleException()
    {
        var mock = new Moq.Mock<IUposCommand>();
        mock.Setup(c => c.Execute()).Throws(new System.Exception("Kaboom"));

        Should.Throw<System.Exception>(() => _mediator.Execute(mock.Object));
        
        _mediator.ResultCode.ShouldBe((int)ErrorCode.Failure);
    }
}
