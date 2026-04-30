using CashChangerSimulator.Device.PosForDotNet.Models;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.PosForDotNet.Services;

/// <summary>UPOS 内部状態の管理とプロパティ解決を担当するクラス。</summary>
public class InternalStatusMonitor(SimulatorContext ctx)
{
    /// <summary>デバイスの現在のステータスを取得します。</summary>
    public CashChangerStatus DeviceStatus => 
        ctx.LifecycleManager.State == ControlState.Closed ? CashChangerStatus.OK : (CashChangerStatus)ctx.StatusCoordinator.LastCashChangerStatus;

    /// <summary>デバイスのフルステータスを取得します。</summary>
    public CashChangerFullStatus FullStatus => 
        ctx.LifecycleManager.State ==
        ControlState.Closed
        ? CashChangerFullStatus.OK
        : (CashChangerFullStatus)ctx.StatusCoordinator.LastFullStatus;

    /// <summary>現在のコントロール状態を UPOS の形式にマップします。</summary>
    public static PosSharp.Abstractions.ControlState MapToControlState(ControlState state) => state switch
    {
        ControlState.Busy =>
            PosSharp.Abstractions.ControlState.Busy,
        ControlState.Closed =>
            PosSharp.Abstractions.ControlState.Closed,
        ControlState.Error =>
            PosSharp.Abstractions.ControlState.Error,
        ControlState.Idle =>
            PosSharp.Abstractions.ControlState.Idle,
        _ => PosSharp.Abstractions.ControlState.Error
    };
}
