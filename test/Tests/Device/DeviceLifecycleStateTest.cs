using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Device.Lifecycle;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>DeviceLifecycleManager（State パターン）の遷移を検証するテストクラス。</summary>
public class DeviceLifecycleStateTest
{
    private readonly HardwareStatusManager HardwareStatusManager = new();
    private readonly Mock<ILogger> _mockLogger = new();
    private bool _deviceEnabled;
    private readonly DeviceLifecycleContext _context;
    private IDeviceState _state;

    /// <summary>DeviceLifecycleStateTest の新しいインスタンスを初期化します。</summary>
    public DeviceLifecycleStateTest()
    {
        _context = new DeviceLifecycleContext(HardwareStatusManager, _mockLogger.Object, v => _deviceEnabled = v);
        _state = new ClosedState();
    }

    /// <summary>Closed 状態で Open を呼ぶと Opened 状態に遷移することを確認します。</summary>
    [Fact]
    public void ClosedStateOpenShouldTransitionToOpened()
    {
        _state = _state.Open(_context);
        _state.ShouldBeOfType<OpenedState>();
        HardwareStatusManager.IsConnected.Value.ShouldBeTrue();
    }

    /// <summary>Closed 状態で Close を呼ぶと例外がスローされることを確認します。</summary>
    [Fact]
    public void ClosedStateCloseShouldThrow()
    {
        Should.Throw<PosControlException>(() => _state.Close(_context));
    }

    /// <summary>Closed 状態で Claim を呼ぶと例外がスローされることを確認します。</summary>
    [Fact]
    public void ClosedStateClaimShouldThrow()
    {
        Should.Throw<PosControlException>(() => _state.Claim(_context, 1000));
    }

    /// <summary>Opened 状態で Claim を呼ぶと Claimed 状態に遷移することを確認します。</summary>
    [Fact]
    public void OpenedStateClaimShouldTransitionToClaimed()
    {
        _state = _state.Open(_context);
        _state = _state.Claim(_context, 1000);
        _state.ShouldBeOfType<ClaimedState>();
    }

    /// <summary>Opened 状態で Close を呼ぶと Closed 状態に戻ることを確認します。</summary>
    [Fact]
    public void OpenedStateCloseShouldTransitionToClosed()
    {
        _state = _state.Open(_context);
        _state = _state.Close(_context);
        _state.ShouldBeOfType<ClosedState>();
        HardwareStatusManager.IsConnected.Value.ShouldBeFalse();
    }

    /// <summary>Claimed 状態で Release を呼ぶと Opened 状態に戻ることを確認します。</summary>
    [Fact]
    public void ClaimedStateReleaseShouldTransitionToOpened()
    {
        _state = _state.Open(_context);
        _state = _state.Claim(_context, 1000);
        _state = _state.Release(_context);
        _state.ShouldBeOfType<OpenedState>();
    }

    /// <summary>Claimed 状態で Close を呼ぶと自動的に Release されて Closed に戻ることを確認します。</summary>
    [Fact]
    public void ClaimedStateCloseShouldReleaseAndTransitionToClosed()
    {
        _state = _state.Open(_context);
        _state = _state.Claim(_context, 1000);
        _state = _state.Close(_context);
        _state.ShouldBeOfType<ClosedState>();
    }

    /// <summary>Opened 状態で重複 Open を呼ぶとそのまま Opened を返すことを確認します。</summary>
    [Fact]
    public void OpenedStateOpenShouldReturnSameState()
    {
        _state = _state.Open(_context);
        var sameState = _state.Open(_context);
        sameState.ShouldBeOfType<OpenedState>();
    }
}
