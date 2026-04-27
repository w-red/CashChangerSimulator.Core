using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Opos;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.PosForDotNet.Strategies;

/// <summary>出金口の現金をクリアする DirectIO コマンド (200)。</summary>
public class TakeCashStrategy : IDirectIOCommand
{
    /// <inheritdoc/>
    public int CommandCode => DirectIOCommands.TakeCash;

    /// <inheritdoc/>
    public DirectIOData Execute(int data, object obj, SimulatorCashChanger device)
    {
        var port = (ExitPort)data;
        device.HardwareStatus.Input.ClearExitPort(port);
        return new DirectIOData(0, obj);
    }
}
