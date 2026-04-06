using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.PosForDotNet;
using CashChangerSimulator.Device.PosForDotNet.Coordination;
using Microsoft.PointOfService;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>UPOS メディエータの状態検証（VerifyState）、コマンド実行、非同期結果処理をテストするクラス。</summary>
public class UposMediatorTests
{
    private readonly InternalSimulatorCashChanger so;
    private readonly IUposMediator mediator;

    public UposMediatorTests()
    {
        so = new InternalSimulatorCashChanger();
        mediator = so.Context.Mediator;
        mediator.SkipStateVerification = false;
    }

    /// <summary>Closed 状態で状態検証を行うと例外が発生することを検証します。</summary>
    [Fact]
    public void VerifyStateShouldThrowClosedWhenClosed()
    {
        // Default state is Closed
        Should.Throw<PosControlException>(() => mediator.VerifyState())
            .ErrorCode.ShouldBe(ErrorCode.Closed);
    }

    /// <summary>占有（Claim）されていない状態で状態検証を行うと例外が発生することを検証します。</summary>
    [Fact]
    public void VerifyStateShouldThrowNotClaimedWhenNotClaimed()
    {
        so.Open();
        Should.Throw<PosControlException>(() => mediator.VerifyState(mustBeClaimed: true))
            .ErrorCode.ShouldBe(ErrorCode.NotClaimed);
    }

    /// <summary>無効（Disabled）状態で状態検証を行うと例外が発生することを検証します。</summary>
    [Fact]
    public void VerifyStateShouldThrowDisabledWhenNotEnabled()
    {
        so.Open();
        so.Claim(0);
        Should.Throw<PosControlException>(() => mediator.VerifyState(mustBeEnabled: true))
            .ErrorCode.ShouldBe(ErrorCode.Disabled);
    }

    /// <summary>ビジー状態での状態検証時に例外が発生することを検証します。</summary>
    [Fact]
    public void VerifyStateShouldThrowBusyWhenBusy()
    {
        so.Open();
        so.Claim(0);
        so.DeviceEnabled = true;
        mediator.IsBusy = true;

        Should.Throw<PosControlException>(() => mediator.VerifyState(mustNotBeBusy: true))
            .ErrorCode.ShouldBe(ErrorCode.Busy);
    }

    /// <summary>全ての条件（Open, Claimed, Enabled, NotBusy）を満たす場合に状態検証が成功することを検証します。</summary>
    [Fact]
    public void VerifyStateShouldNotThrowWhenAllConditionsMet()
    {
        so.Open();
        so.Claim(0);
        so.DeviceEnabled = true;
        mediator.IsBusy = false;

        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: true, mustNotBeBusy: true);
    }

    /// <summary>検証スキップフラグが有効な場合に、不正な状態でも検証が成功することを検証します。</summary>
    [Fact]
    public void VerifyStateShouldSkipWhenSkipFlagIsSet()
    {
        mediator.SkipStateVerification = true;
        mediator.VerifyState(); // Should not throw even if Closed
    }

    /// <summary>ThrowIfBusy メソッドがビジー判定時に例外をスローすることを検証します。</summary>
    [Fact]
    public void ThrowIfBusyShouldThrowWhenBusy()
    {
        Should.Throw<PosControlException>(() => UposMediator.ThrowIfBusy(true))
            .ErrorCode.ShouldBe(ErrorCode.Busy);
    }

    /// <summary>ThrowIfDepositInProgress メソッドが入金中判定時に例外をスローすることを検証します。</summary>
    [Fact]
    public void ThrowIfDepositInProgressShouldThrowWhenInProgress()
    {
        Should.Throw<PosControlException>(() => UposMediator.ThrowIfDepositInProgress(true))
            .ErrorCode.ShouldBe(ErrorCode.Illegal);
    }

    /// <summary>SetSuccess および SetFailure により ResultCode 等が正しく更新されることを検証します。</summary>
    [Fact]
    public void SetSuccessAndFailureShouldUpdateProperties()
    {
        mediator.SetSuccess();
        mediator.ResultCode.ShouldBe((int)ErrorCode.Success);

        mediator.SetFailure(ErrorCode.Illegal, 456);
        mediator.ResultCode.ShouldBe((int)ErrorCode.Illegal);
        mediator.ResultCodeExtended.ShouldBe(456);
    }

    /// <summary>コマンド実行中に PosControlException が発生した場合に、ResultCode 等が適切にキャッチ・設定されることを検証します。</summary>
    [Fact]
    public void ExecuteShouldHandlePosControlException()
    {
        var mock = new Mock<IUposCommand>();
        mock.Setup(c => c.Verify(It.IsAny<IUposMediator>()));
        mock.Setup(c => c.Execute()).Throws(new PosControlException("Pos error", ErrorCode.Illegal, 789));

        var ex = Should.Throw<PosControlException>(() => mediator.Execute(mock.Object));

        mediator.ResultCode.ShouldBe((int)ErrorCode.Illegal);
        mediator.ResultCodeExtended.ShouldBe(789);
        ex.ErrorCode.ShouldBe((ErrorCode)DeviceErrorCode.Illegal);
    }

    /// <summary>コマンドの正常実行が成功し、ResultCode が Success になることを検証します。</summary>
    [Fact]
    public void ExecuteShouldSucceed()
    {
        var mock = new Mock<IUposCommand>();
        mediator.Execute(mock.Object);
        mediator.ResultCode.ShouldBe((int)ErrorCode.Success);
    }
}
