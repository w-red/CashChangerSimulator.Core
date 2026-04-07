using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Device.PosForDotNet.Lifecycle;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PointOfService;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>デバイスのライフサイクル状態（Closed, Opened, Claimed）の遷移ロジックを個別検証するテストクラス。</summary>
public class LifecycleStateTests
{
    private readonly DeviceLifecycleContext context;
    private bool deviceEnabled;

    public LifecycleStateTests()
    {
        var hw = HardwareStatusManager.Create();
        context = new DeviceLifecycleContext(hw, NullLogger.Instance, enabled => deviceEnabled = enabled);
    }

    /// <summary>ClosedState からの各操作による状態遷移と例外発生を検証します。</summary>
    [Fact]
    public void ClosedStateTransitions()
    {
        var state = new ClosedState();

        // Open
        state.Open(context).ShouldBeOfType<OpenedState>();
        context.HardwareStatusManager.IsConnected.Value.ShouldBeTrue();

        // Already closed or invalid operations
        Should.Throw<PosControlException>(() => state.Close(context)).ErrorCode.ShouldBe(ErrorCode.Closed);
        Should.Throw<PosControlException>(() => state.Claim(context, 0)).ErrorCode.ShouldBe(ErrorCode.Closed);
        Should.Throw<PosControlException>(() => state.Release(context)).ErrorCode.ShouldBe(ErrorCode.Closed);
    }

    /// <summary>OpenedState からの各操作による状態遷移（Claim, Close等）を検証します。</summary>
    [Fact]
    public void OpenedStateTransitions()
    {
        var state = new OpenedState();

        // Already open
        state.Open(context).ShouldBe(state);

        // Claim
        state.Claim(context, 0).ShouldBeOfType<ClaimedState>();

        // Close
        context.HardwareStatusManager.SetConnected(true);
        state.Close(context).ShouldBeOfType<ClosedState>();
        context.HardwareStatusManager.IsConnected.Value.ShouldBeFalse();

        // Release (ignored)
        state.Release(context).ShouldBe(state);
    }

    /// <summary>ClaimedState からの解放（Release）および自動解放を伴う Close 操作を検証します。</summary>
    [Fact]
    public void ClaimedStateTransitions()
    {
        var state = new ClaimedState();

        // Already open/claimed
        state.Open(context).ShouldBe(state);
        state.Claim(context, 0).ShouldBe(state);

        // Release
        deviceEnabled = true;
        state.Release(context).ShouldBeOfType<OpenedState>();
        deviceEnabled.ShouldBeFalse();

        // Close (auto-release)
        context.HardwareStatusManager.SetConnected(true);
        deviceEnabled = true;
        state.Close(context).ShouldBeOfType<ClosedState>();
        deviceEnabled.ShouldBeFalse();
        context.HardwareStatusManager.IsConnected.Value.ShouldBeFalse();
    }
}
