using CashChangerSimulator.Core.Opos;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.Strategies;

/// <summary>メカニカルジャム（Jam）エラー状態を設定または解除する戦略。</summary>
public class SetJamStrategy : IDirectIOCommand
{
    public int CommandCode => DirectIOCommands.SetJam;

    public DirectIOData Execute(int data, object obj, SimulatorCashChanger device)
    {
        device._hardwareStatusManager.SetJammed(data != 0);
        return new DirectIOData(data, obj);
    }
}
