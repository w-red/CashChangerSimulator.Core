using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Device.Lifecycle;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PointOfService;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>デバイスのライフサイクル状態（Closed, Opened, Claimed）の遷移ロジックを個別検証するテストクラス。</summary>
public class LifecycleStateTests
{
    private readonly DeviceLifecycleContext _context;
    private bool _deviceEnabled;

    public LifecycleStateTests()
    {
        var hw = new HardwareStatusManager();
        _context = new DeviceLifecycleContext(hw, NullLogger.Instance, enabled => _deviceEnabled = enabled);
    }

    /// <summary>ClosedState からの各操作による状態遷移と例外発生を検証します。</summary>
    [Fact]
    public void ClosedState_Transitions()
    {
        var state = new ClosedState();
        
        // Open
        state.Open(_context).ShouldBeOfType<OpenedState>();
        _context.HardwareStatusManager.IsConnected.Value.ShouldBeTrue();

        // Already closed or invalid operations
        Should.Throw<PosControlException>(() => state.Close(_context)).ErrorCode.ShouldBe(ErrorCode.Closed);
        Should.Throw<PosControlException>(() => state.Claim(_context, 0)).ErrorCode.ShouldBe(ErrorCode.Closed);
        Should.Throw<PosControlException>(() => state.Release(_context)).ErrorCode.ShouldBe(ErrorCode.Closed);
    }

    /// <summary>OpenedState からの各操作による状態遷移（Claim, Close等）を検証します。</summary>
    [Fact]
    public void OpenedState_Transitions()
    {
        var state = new OpenedState();

        // Already open
        state.Open(_context).ShouldBe(state);
        
        // Claim
        state.Claim(_context, 0).ShouldBeOfType<ClaimedState>();

        // Close
        _context.HardwareStatusManager.SetConnected(true);
        state.Close(_context).ShouldBeOfType<ClosedState>();
        _context.HardwareStatusManager.IsConnected.Value.ShouldBeFalse();

        // Release (ignored)
        state.Release(_context).ShouldBe(state);
    }

    /// <summary>ClaimedState からの解放（Release）および自動解放を伴う Close 操作を検証します。</summary>
    [Fact]
    public void ClaimedState_Transitions()
    {
        var state = new ClaimedState();

        // Already open/claimed
        state.Open(_context).ShouldBe(state);
        state.Claim(_context, 0).ShouldBe(state);

        // Release
        _deviceEnabled = true;
        state.Release(_context).ShouldBeOfType<OpenedState>();
        _deviceEnabled.ShouldBeFalse();

        // Close (auto-release)
        _context.HardwareStatusManager.SetConnected(true);
        _deviceEnabled = true;
        state.Close(_context).ShouldBeOfType<ClosedState>();
        _deviceEnabled.ShouldBeFalse();
        _context.HardwareStatusManager.IsConnected.Value.ShouldBeFalse();
    }
}
