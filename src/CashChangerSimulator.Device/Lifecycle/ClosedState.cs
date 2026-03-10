using Microsoft.PointOfService;
using ZLogger;

namespace CashChangerSimulator.Device.Lifecycle;

/// <summary>デバイスが未接続（Closed）の状態を表します。</summary>
public class ClosedState : IDeviceState
{
    /// <inheritdoc/>
    public IDeviceState Open(DeviceLifecycleContext context)
    {
        context.HardwareStatusManager.SetConnected(true);
        context.Logger.ZLogInformation($"OPOS Open called via simulator.");
        return new OpenedState();
    }

    /// <inheritdoc/>
    public IDeviceState Close(DeviceLifecycleContext context)
    {
        throw new PosControlException("Device is not open.", ErrorCode.Closed);
    }

    /// <inheritdoc/>
    public IDeviceState Claim(DeviceLifecycleContext context, int timeout)
    {
        throw new PosControlException("Device must be opened before claiming.", ErrorCode.Closed);
    }

    /// <inheritdoc/>
    public IDeviceState Release(DeviceLifecycleContext context)
    {
        throw new PosControlException("Device is not claimed.", ErrorCode.Illegal);
    }
}
