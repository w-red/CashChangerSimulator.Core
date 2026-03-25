using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Coordination;
using Microsoft.PointOfService;
using CashChangerSimulator.Core.Opos;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>UPOS メディエータの状態検証（VerifyState）、コマンド実行、非同期結果処理をテストするクラス。</summary>
public class UposMediatorTests
{
    private readonly InternalSimulatorCashChanger _so;
    private readonly UposMediator _mediator;

    public UposMediatorTests()
    {
        _so = new InternalSimulatorCashChanger();
        _mediator = new UposMediator(_so);
    }

    /// <summary>Closed 状態で状態検証を行うと例外が発生することを検証します。</summary>
    [Fact]
    public void VerifyState_ShouldThrowClosed_WhenClosed()
    {
        // Default state is Closed
        Should.Throw<PosControlException>(() => _mediator.VerifyState())
            .ErrorCode.ShouldBe(ErrorCode.Closed);
    }

    /// <summary>占有（Claim）されていない状態で状態検証を行うと例外が発生することを検証します。</summary>
    [Fact]
    public void VerifyState_ShouldThrowNotClaimed_WhenNotClaimed()
    {
        _so.Open();
        Should.Throw<PosControlException>(() => _mediator.VerifyState(mustBeClaimed: true))
            .ErrorCode.ShouldBe(ErrorCode.NotClaimed);
    }

    /// <summary>無効（Disabled）状態で状態検証を行うと例外が発生することを検証します。</summary>
    [Fact]
    public void VerifyState_ShouldThrowDisabled_WhenNotEnabled()
    {
        _so.Open();
        _so.Claim(0);
        Should.Throw<PosControlException>(() => _mediator.VerifyState(mustBeEnabled: true))
            .ErrorCode.ShouldBe(ErrorCode.Disabled);
    }

    /// <summary>ビジー状態での状態検証時に例外が発生することを検証します。</summary>
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

    /// <summary>全ての条件（Open, Claimed, Enabled, NotBusy）を満たす場合に状態検証が成功することを検証します。</summary>
    [Fact]
    public void VerifyState_ShouldNotThrow_WhenAllConditionsMet()
    {
        _so.Open();
        _so.Claim(0);
        _so.DeviceEnabled = true;
        _mediator.IsBusy = false;

        _mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: true, mustNotBeBusy: true);
    }

    /// <summary>検証スキップフラグが有効な場合に、不正な状態でも検証が成功することを検証します。</summary>
    [Fact]
    public void VerifyState_ShouldSkip_WhenSkipFlagIsSet()
    {
        _mediator.SkipStateVerification = true;
        _mediator.VerifyState(); // Should not throw even if Closed
    }

    /// <summary>ThrowIfBusy メソッドがビジー判定時に例外をスローすることを検証します。</summary>
    [Fact]
    public void ThrowIfBusy_ShouldThrow_WhenBusy()
    {
        Should.Throw<PosControlException>(() => UposMediator.ThrowIfBusy(true))
            .ErrorCode.ShouldBe(ErrorCode.Busy);
    }

    /// <summary>ThrowIfDepositInProgress メソッドが入金中判定時に例外をスローすることを検証します。</summary>
    [Fact]
    public void ThrowIfDepositInProgress_ShouldThrow_WhenInProgress()
    {
        Should.Throw<PosControlException>(() => UposMediator.ThrowIfDepositInProgress(true))
            .ErrorCode.ShouldBe(ErrorCode.Illegal);
    }

    /// <summary>非同期出金処理の結果が正常に内部プロパティへ反映され、完了イベントが発火することを検証します。</summary>
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

    /// <summary>同期出金処理の結果が内部プロパティへ反映され、完了イベントが発火しないことを検証します。</summary>
    [Fact]
    public void HandleDispenseResult_ShouldNotFireEvent_WhenSync()
    {
        bool eventFired = false;
        _so.OnEventQueued = (e) => eventFired = true;

        _mediator.HandleDispenseResult(ErrorCode.Success, 0, false);

        _mediator.ResultCode.ShouldBe((int)ErrorCode.Success);
        _mediator.IsBusy.ShouldBeFalse();
        eventFired.ShouldBeFalse();
    }

    /// <summary>SetSuccess および SetFailure により ResultCode 等が正しく更新されることを検証します。</summary>
    [Fact]
    public void SetSuccessAndFailure_ShouldUpdateProperties()
    {
        _mediator.SetSuccess();
        _mediator.ResultCode.ShouldBe((int)ErrorCode.Success);

        _mediator.SetFailure(ErrorCode.Illegal, 456);
        _mediator.ResultCode.ShouldBe((int)ErrorCode.Illegal);
        _mediator.ResultCodeExtended.ShouldBe(456);
    }

    /// <summary>コマンド実行中に PosControlException が発生した場合に、ResultCode 等が適切にキャッチ・設定されることを検証します。</summary>
    [Fact]
    public void Execute_ShouldHandlePosControlException()
    {
        var mock = new Mock<IUposCommand>();
        mock.Setup(c => c.Verify(It.IsAny<IUposMediator>()));
        mock.Setup(c => c.Execute()).Throws(new PosControlException("Pos error", ErrorCode.Illegal, 789));

        var ex = Should.Throw<PosControlException>(() => _mediator.Execute(mock.Object));
        
        _mediator.ResultCode.ShouldBe((int)ErrorCode.Illegal);
        _mediator.ResultCodeExtended.ShouldBe(789);
        ex.ErrorCode.ShouldBe(ErrorCode.Illegal);
    }

    /// <summary>コマンドの正常実行が成功し、ResultCode が Success になることを検証します。</summary>
    [Fact]
    public void Execute_ShouldSucceed()
    {
        var mock = new Mock<IUposCommand>();
        _mediator.Execute(mock.Object);
        _mediator.ResultCode.ShouldBe((int)ErrorCode.Success);
    }
}
