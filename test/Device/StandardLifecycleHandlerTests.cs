using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device.PosForDotNet.Coordination;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PointOfService;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>標準的なライフサイクルハンドラの状態遷移、Open/Claim/Close/Release 操作を検証するテストクラス。</summary>
public class StandardLifecycleHandlerTests
{
    private readonly HardwareStatusManager hardware;
    private readonly Mock<IUposMediator> mediator;
    private readonly TransactionHistory history;
    private readonly StandardLifecycleHandler handler;

    public StandardLifecycleHandlerTests()
    {
        hardware = HardwareStatusManager.Create();
        mediator = new Mock<IUposMediator>();
        mediator.SetupAllProperties(); // [FIX] Ensure properties act as variables
        history = new TransactionHistory();
        handler = new StandardLifecycleHandler(hardware, mediator.Object, history, NullLogger.Instance);
    }

    /// <summary>ハンドラの状態がハードウェア接続およびメディエータのビジー状態を反映することを検証します。</summary>
    [Fact]
    public void StateShouldReflectHardwareAndMediator()
    {
        // Closed
        hardware.Input.IsConnected.Value = false;
        handler.State.ShouldBe(ControlState.Closed);

        // Idle
        hardware.Input.IsConnected.Value = true;
        mediator.Setup(m => m.IsBusy).Returns(false);
        handler.State.ShouldBe(ControlState.Idle);

        // Busy
        mediator.Setup(m => m.IsBusy).Returns(true);
        handler.State.ShouldBe(ControlState.Busy);
    }

    /// <summary>Closed 状態で DeviceEnabled を設定しようとした際に例外が発生することを検証します。</summary>
    [Fact]
    public void DeviceEnabledSetterShouldThrowWhenClosed()
    {
        hardware.Input.IsConnected.Value = false;
        Should.Throw<PosControlException>(() => handler.DeviceEnabled = true)
            .ErrorCode.ShouldBe(ErrorCode.Closed);
    }

    /// <summary>Unclaimed 状態で DeviceEnabled を設定しようとした際に例外が発生することを検証します。</summary>
    [Fact]
    public void DeviceEnabledSetterShouldThrowWhenNotClaimed()
    {
        hardware.Input.IsConnected.Value = true;
        mediator.Setup(m => m.Claimed).Returns(false);
        Should.Throw<PosControlException>(() => handler.DeviceEnabled = true)
            .ErrorCode.ShouldBe(ErrorCode.NotClaimed);
    }

    /// <summary>他者に Claim されている状態で DeviceEnabled を設定しようとした際に例外が発生することを検証します。</summary>
    [Fact]
    public void DeviceEnabledSetterShouldThrowWhenClaimedByAnother()
    {
        hardware.Input.IsConnected.Value = true;
        hardware.Input.IsClaimedByAnother.Value = true;
        Should.Throw<PosControlException>(() => handler.DeviceEnabled = true)
            .ErrorCode.ShouldBe(ErrorCode.Claimed);
    }

    /// <summary>Claim されている状態で DeviceEnabled が正常に設定できることを検証します。</summary>
    [Fact]
    public void DeviceEnabledSetterShouldWorkWhenClaimed()
    {
        hardware.Input.IsConnected.Value = true;
        mediator.Setup(m => m.Claimed).Returns(true);
        mediator.SetupProperty(m => m.DeviceEnabled);

        handler.DeviceEnabled = true;
        mediator.Object.DeviceEnabled.ShouldBeTrue();
    }

    /// <summary>既に Open されている状態での Open 呼び出しが正常に処理されることを検証します。</summary>
    [Fact]
    public void OpenShouldHandleAlreadyOpen()
    {
        hardware.Input.IsConnected.Value = true;
        var baseCalled = false;
        handler.Open(() => baseCalled = true);

        baseCalled.ShouldBeFalse();
        mediator.Verify(m => m.SetSuccess(), Times.Once);
    }

    /// <summary>Open 処理中の例外発生時に、ハードウェア接続状態が正しく更新されることを検証します。</summary>
    [Fact]
    public void OpenShouldHandleBaseException()
    {
        hardware.Input.IsConnected.Value = false;
        handler.Open(() => throw new Exception("Test"));

        hardware.IsConnected.CurrentValue.ShouldBeTrue();
        history.Entries.ShouldContain(e => e.Type == TransactionType.Open);
    }

    /// <summary>既に Closed 状態での Close 呼び出しが正常に処理されることを検証します。</summary>
    [Fact]
    public void CloseShouldHandleAlreadyClosed()
    {
        hardware.Input.IsConnected.Value = false;
        var baseCalled = false;
        handler.Close(() => baseCalled = true);

        baseCalled.ShouldBeFalse();
        mediator.Verify(m => m.SetSuccess(), Times.Once);
    }

    /// <summary>Close 処理中の例外発生時に、暗黙的な Release と Close が行われることを検証します。</summary>
    [Fact]
    public void CloseShouldHandleBaseExceptionAndImplicitRelease()
    {
        hardware.Input.IsConnected.Value = true;
        mediator.Setup(m => m.Claimed).Returns(true);

        handler.Close(() => throw new Exception("Test"));

        hardware.IsConnected.CurrentValue.ShouldBeFalse();
        history.Entries.ShouldContain(e => e.Type == TransactionType.Release);
        history.Entries.ShouldContain(e => e.Type == TransactionType.Close);
    }

    /// <summary>Closed 状態で Claim を試みた際に例外が発生することを検証します。</summary>
    [Fact]
    public void ClaimShouldThrowWhenClosed()
    {
        hardware.Input.IsConnected.Value = false;
        Should.Throw<PosControlException>(() => handler.Claim(0, _ => { }))
            .ErrorCode.ShouldBe(ErrorCode.Closed);
    }

    /// <summary>既に Claim されている状態での Claim 呼び出しが正常に処理されることを検証します。</summary>
    [Fact]
    public void ClaimShouldHandleAlreadyClaimed()
    {
        hardware.Input.IsConnected.Value = true;
        mediator.Setup(m => m.Claimed).Returns(true);

        var baseCalled = false;
        handler.Claim(0, _ => baseCalled = true);

        baseCalled.ShouldBeFalse();
    }

    /// <summary>Claim 処理中の例外発生時に、メディエータの状態が正しく更新されることを検証します。</summary>
    [Fact]
    public void ClaimShouldHandleBaseException()
    {
        hardware.Input.IsConnected.Value = true;
        mediator.Setup(m => m.Claimed).Returns(false);

        handler.Claim(0, _ => throw new Exception("Test"));

        mediator.VerifySet(m => m.Claimed = true);
        history.Entries.ShouldContain(e => e.Type == TransactionType.Claim);
    }

    /// <summary>Closed 状態で Release を試みた際に例外が発生することを検証します。</summary>
    [Fact]
    public void ReleaseShouldThrowWhenClosed()
    {
        hardware.Input.IsConnected.Value = false;
        Should.Throw<PosControlException>(() => handler.Release(() => { }))
            .ErrorCode.ShouldBe(ErrorCode.Closed);
    }

    /// <summary>Claim されていない状態での Release 呼び出しが正常に処理されることを検証します。</summary>
    [Fact]
    public void ReleaseShouldHandleNotClaimed()
    {
        hardware.Input.IsConnected.Value = true;
        mediator.Setup(m => m.Claimed).Returns(false);

        var baseCalled = false;
        handler.Release(() => baseCalled = true);

        baseCalled.ShouldBeFalse();
    }

    /// <summary>Release 処理中の例外発生時に、メディエータの状態が正しく更新されることを検証します。</summary>
    [Fact]
    public void ReleaseShouldHandleBaseException()
    {
        hardware.Input.IsConnected.Value = true;
        mediator.Setup(m => m.Claimed).Returns(true);

        handler.Release(() => throw new Exception("Test"));

        mediator.VerifySet(m => m.Claimed = false);
        history.Entries.ShouldContain(e => e.Type == TransactionType.Release);
    }
}
