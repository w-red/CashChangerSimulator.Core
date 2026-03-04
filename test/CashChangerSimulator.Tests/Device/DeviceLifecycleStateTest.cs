using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Device.Lifecycle;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using Moq;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Tests.Device;

/// <summary>DeviceLifecycleManager（State パターン）の遷移を検証するテストクラス。</summary>
public class DeviceLifecycleStateTest
{
    private readonly HardwareStatusManager _hardwareStatusManager = new();
    private readonly Mock<ILogger> _mockLogger = new();
    private bool _deviceEnabled;
    private DeviceLifecycleContext _context;
    private IDeviceState _state;

    public DeviceLifecycleStateTest()
    {
        _context = new DeviceLifecycleContext(_hardwareStatusManager, _mockLogger.Object, v => _deviceEnabled = v);
        _state = new ClosedState();
    }

    /// <summary>Closed 状態で Open を呼ぶと Opened 状態に遷移することを確認します。</summary>
    [Fact]
    public void ClosedState_Open_ShouldTransitionToOpened()
    {
        _state = _state.Open(_context);
        _state.ShouldBeOfType<OpenedState>();
        _hardwareStatusManager.IsConnected.Value.ShouldBeTrue();
    }

    /// <summary>Closed 状態で Close を呼ぶと例外がスローされることを確認します。</summary>
    [Fact]
    public void ClosedState_Close_ShouldThrow()
    {
        Should.Throw<PosControlException>(() => _state.Close(_context));
    }

    /// <summary>Closed 状態で Claim を呼ぶと例外がスローされることを確認します。</summary>
    [Fact]
    public void ClosedState_Claim_ShouldThrow()
    {
        Should.Throw<PosControlException>(() => _state.Claim(_context, 1000));
    }

    /// <summary>Opened 状態で Claim を呼ぶと Claimed 状態に遷移することを確認します。</summary>
    [Fact]
    public void OpenedState_Claim_ShouldTransitionToClaimed()
    {
        _state = _state.Open(_context);
        _state = _state.Claim(_context, 1000);
        _state.ShouldBeOfType<ClaimedState>();
    }

    /// <summary>Opened 状態で Close を呼ぶと Closed 状態に戻ることを確認します。</summary>
    [Fact]
    public void OpenedState_Close_ShouldTransitionToClosed()
    {
        _state = _state.Open(_context);
        _state = _state.Close(_context);
        _state.ShouldBeOfType<ClosedState>();
        _hardwareStatusManager.IsConnected.Value.ShouldBeFalse();
    }

    /// <summary>Claimed 状態で Release を呼ぶと Opened 状態に戻ることを確認します。</summary>
    [Fact]
    public void ClaimedState_Release_ShouldTransitionToOpened()
    {
        _state = _state.Open(_context);
        _state = _state.Claim(_context, 1000);
        _state = _state.Release(_context);
        _state.ShouldBeOfType<OpenedState>();
    }

    /// <summary>Claimed 状態で Close を呼ぶと自動的に Release されて Closed に戻ることを確認します。</summary>
    [Fact]
    public void ClaimedState_Close_ShouldReleaseAndTransitionToClosed()
    {
        _state = _state.Open(_context);
        _state = _state.Claim(_context, 1000);
        _state = _state.Close(_context);
        _state.ShouldBeOfType<ClosedState>();
    }

    /// <summary>Opened 状態で重複 Open を呼ぶとそのまま Opened を返すことを確認します。</summary>
    [Fact]
    public void OpenedState_Open_ShouldReturnSameState()
    {
        _state = _state.Open(_context);
        var sameState = _state.Open(_context);
        sameState.ShouldBeOfType<OpenedState>();
    }
}
