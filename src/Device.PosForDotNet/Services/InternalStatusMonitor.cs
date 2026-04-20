using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device.PosForDotNet.Coordination;
using CashChangerSimulator.Device.PosForDotNet.Models;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.PosForDotNet.Services;

/// <summary>UPOS 内部状態の管理とプロパティ解決を担当するクラス。</summary>
public class InternalStatusMonitor(SimulatorContext ctx)
{
    /// <summary>デバイスの現在のステータスを取得します。</summary>
    public CashChangerStatus DeviceStatus => 
        ctx.LifecycleManager.State == ControlState.Closed ? CashChangerStatus.OK : ctx.StatusCoordinator.LastCashChangerStatus;

    /// <summary>デバイスのフルステータスを取得します。</summary>
    public CashChangerFullStatus FullStatus => 
        ctx.LifecycleManager.State == ControlState.Closed ? CashChangerFullStatus.OK : ctx.StatusCoordinator.LastFullStatus;

    /// <summary>現在のコントロール状態を UPOS の形式にマップします。</summary>
    public static DeviceControlState MapToDeviceControlState(ControlState state) => state switch
    {
        ControlState.Busy => DeviceControlState.Busy,
        ControlState.Closed => DeviceControlState.Closed,
        ControlState.Error => DeviceControlState.Error,
        ControlState.Idle => DeviceControlState.Idle,
        _ => DeviceControlState.Error
    };
}
