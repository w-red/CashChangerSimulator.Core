using System;

namespace CashChangerSimulator.Device.Lifecycle;

/// <summary>デバイスのライフサイクル状態を表すインターフェース。</summary>
public interface IDeviceState
{
    /// <summary>Open 操作を実行し、次の状態を返します。</summary>
    IDeviceState Open(DeviceLifecycleContext context);

    /// <summary>Close 操作を実行し、次の状態を返します。</summary>
    IDeviceState Close(DeviceLifecycleContext context);

    /// <summary>Claim 操作を実行し、次の状態を返します。</summary>
    IDeviceState Claim(DeviceLifecycleContext context, int timeout);

    /// <summary>Release 操作を実行し、次の状態を返します。</summary>
    IDeviceState Release(DeviceLifecycleContext context);
}
