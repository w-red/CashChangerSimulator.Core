using Microsoft.Extensions.Logging;
using ZLogger;

namespace CashChangerSimulator.Device.Lifecycle;

/// <summary>デバイスが接続済み（Opened）で、未 Claim の状態を表します。</summary>
public class OpenedState : IDeviceState
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
        context.SetDeviceEnabled(false);
        context.HardwareStatusManager.SetConnected(false);
        context.Logger.ZLogInformation($"OPOS Close called via simulator.");
        return new ClosedState();
    }

    /// <inheritdoc/>
    public IDeviceState Claim(DeviceLifecycleContext context, int timeout)
    {
        context.Logger.ZLogInformation($"OPOS Claim({timeout}) called via simulator.");
        return new ClaimedState();
    }

    /// <inheritdoc/>
    public IDeviceState Release(DeviceLifecycleContext context)
    {
        context.Logger.LogWarning("Device is not claimed. Release ignored.");
        return this;
    }
}
