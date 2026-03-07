using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Device.Testing;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.Strategies;

/// <summary>デバイスの装着(INSERTED)をエミュレートする戦略。</summary>
public class SimulateInsertedStrategy : IDirectIOCommand
{
    public int CommandCode => DirectIOCommands.SimulateInserted;

    public DirectIOData Execute(int data, object obj, SimulatorCashChanger device)
    {
        device.FireEvent(new StatusUpdateEventArgs((int)UposCashChangerStatusUpdateCode.Inserted));
        return new DirectIOData(data, "INSERTED");
    }
}
