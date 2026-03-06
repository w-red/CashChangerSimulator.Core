using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.Services;

/// <summary>デバイスの現在の UPOS 状態を提供するインターフェース。</summary>
public interface IDeviceStateProvider
{
    /// <summary>現在のデバイス状態を取得します。</summary>
    ControlState State { get; }
}
