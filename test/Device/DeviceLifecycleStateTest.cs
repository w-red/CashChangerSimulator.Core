using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Device.PosForDotNet.Lifecycle;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>DeviceLifecycleManager（State パターン）の遷移を検証するテストクラス。</summary>
public class DeviceLifecycleStateTest
{
    private readonly HardwareStatusManager hardwareStatusManager = HardwareStatusManager.Create();
    private readonly Mock<ILogger> mockLogger = new();
    private bool deviceEnabled;
    private readonly DeviceLifecycleContext context;
    private IDeviceState state;

    /// <summary>Initializes a new instance of the <see cref="DeviceLifecycleStateTest"/> class.DeviceLifecycleStateTest の新しいインスタンスを初期化します。</summary>
    public DeviceLifecycleStateTest()
    {
        context = new DeviceLifecycleContext(hardwareStatusManager, mockLogger.Object, v => deviceEnabled = v);
        state = new ClosedState();
    }

    /// <summary>Closed 状態で Open を呼ぶと Opened 状態に遷移することを確認します。</summary>
    [Fact]
    public void ClosedStateOpenShouldTransitionToOpened()
    {
        state = state.Open(context);
        state.ShouldBeOfType<OpenedState>();
        hardwareStatusManager.IsConnected.CurrentValue.ShouldBeTrue();
    }

    /// <summary>Closed 状態で Close を呼ぶと例外がスローされることを確認します。</summary>
    [Fact]
    public void ClosedStateCloseShouldThrow()
    {
        Should.Throw<PosControlException>(() => state.Close(context));
    }

    /// <summary>Closed 状態で Claim を呼ぶと例外がスローされることを確認します。</summary>
    [Fact]
    public void ClosedStateClaimShouldThrow()
    {
        Should.Throw<PosControlException>(() => state.Claim(context, 1000));
    }

    /// <summary>Opened 状態で Claim を呼ぶと Claimed 状態に遷移することを確認します。</summary>
    [Fact]
    public void OpenedStateClaimShouldTransitionToClaimed()
    {
        state = state.Open(context);
        state = state.Claim(context, 1000);
        state.ShouldBeOfType<ClaimedState>();
    }

    /// <summary>Opened 状態で Close を呼ぶと Closed 状態に戻ることを確認します。</summary>
    [Fact]
    public void OpenedStateCloseShouldTransitionToClosed()
    {
        state = state.Open(context);
        state = state.Close(context);
        state.ShouldBeOfType<ClosedState>();
        hardwareStatusManager.IsConnected.CurrentValue.ShouldBeFalse();
    }

    /// <summary>Claimed 状態で Release を呼ぶと Opened 状態に戻ることを確認します。</summary>
    [Fact]
    public void ClaimedStateReleaseShouldTransitionToOpened()
    {
        state = state.Open(context);
        state = state.Claim(context, 1000);
        state = state.Release(context);
        state.ShouldBeOfType<OpenedState>();
    }

    /// <summary>Claimed 状態で Close を呼ぶと自動的に Release されて Closed に戻ることを確認します。</summary>
    [Fact]
    public void ClaimedStateCloseShouldReleaseAndTransitionToClosed()
    {
        state = state.Open(context);
        state = state.Claim(context, 1000);
        state = state.Close(context);
        state.ShouldBeOfType<ClosedState>();
    }

    /// <summary>Opened 状態で重複 Open を呼ぶとそのまま Opened を返すことを確認します。</summary>
    [Fact]
    public void OpenedStateOpenShouldReturnSameState()
    {
        state = state.Open(context);
        var sameState = state.Open(context);
        sameState.ShouldBeOfType<OpenedState>();
    }
}
