using ZLogger;
using Microsoft.Extensions.Logging;

namespace CashChangerSimulator.Device.Lifecycle;

/// <summary>デバイスが Claim 済みの状態を表します。</summary>
public class ClaimedState : IDeviceState
{
    /// <inheritdoc/>
    public IDeviceState Open(DeviceLifecycleContext context)
    {
        context.Logger.LogWarning("Device is already open.");
        return this;
    }

    /// <inheritdoc/>
    public IDeviceState Close(DeviceLifecycleContext context)
    {
        // Auto-release before closing
        context.SetDeviceEnabled(false);
        context.HardwareStatusManager.SetConnected(false);
        context.Logger.ZLogInformation($"OPOS Close called via simulator (auto-released).");
        return new ClosedState();
    }

    /// <inheritdoc/>
    public IDeviceState Claim(DeviceLifecycleContext context, int timeout)
    {
        context.Logger.LogWarning("Device is already claimed.");
        return this;
    }

    /// <inheritdoc/>
    public IDeviceState Release(DeviceLifecycleContext context)
    {
        context.SetDeviceEnabled(false);
        context.Logger.ZLogInformation($"OPOS Release called via simulator.");
        return new OpenedState();
    }
}
