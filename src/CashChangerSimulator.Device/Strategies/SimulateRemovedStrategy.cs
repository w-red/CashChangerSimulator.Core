using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Device.Testing;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.Strategies;

/// <summary>デバイスの取り外し(REMOVED)をエミュレートする戦略。</summary>
public class SimulateRemovedStrategy : IDirectIOCommand
{
    public int CommandCode => DirectIOCommands.SimulateRemoved;

    public DirectIOData Execute(int data, object obj, SimulatorCashChanger device)
    {
        // SimulatorCashChanger.NotifyEvent は protected なので、外部からは QueueEvent を呼ぶか、
        // SimulatorCashChanger に通知用の public/internal メソッドを用意する必要がある。
        // ここでは SimulatorCashChanger に追加した NotifyStatusUpdate メソッド（後述）を呼ぶ想定。
        device.FireEvent(new StatusUpdateEventArgs((int)UposCashChangerStatusUpdateCode.Removed));
        return new DirectIOData(data, "REMOVED");
    }
}
