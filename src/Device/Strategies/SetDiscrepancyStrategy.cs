using CashChangerSimulator.Core.Opos;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.Strategies;

/// <summary>不一致(Discrepancy)が発生した状態を強制設定する戦略。</summary>
public class SetDiscrepancyStrategy : IDirectIOCommand
{
    public int CommandCode => DirectIOCommands.SetDiscrepancy;

    public DirectIOData Execute(int data, object obj, SimulatorCashChanger device)
    {
        device.Inventory.HasDiscrepancy = (data != 0);
        return new DirectIOData(data, obj);
    }
}
