using CashChangerSimulator.Device;

namespace CashChangerSimulator.Device.Virtual.Services;

/// <summary>デバイスの現在の状態を提供するインターフェース。</summary>
public interface IDeviceStateProvider
{
    /// <summary>現在のデバイス状態を取得します。</summary>
    DeviceControlState State { get; }
}
