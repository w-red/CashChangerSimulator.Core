using CashChangerSimulator.Core.Opos;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.Strategies;

/// <summary>重複投入（Overlap）エラー状態を設定または解除する戦略。</summary>
public class SetOverlapStrategy : IDirectIOCommand
{
    public int CommandCode => DirectIOCommands.SetOverlap;

    public DirectIOData Execute(int data, object obj, SimulatorCashChanger device)
    {
        device._hardwareStatusManager.SetOverlapped(data != 0);
        return new DirectIOData(data, obj);
    }
}
