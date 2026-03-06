using CashChangerSimulator.Core.Opos;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.Strategies;

/// <summary>診断ログを取得するための DirectIOStrategy。</summary>
public class GetDiagnosticLogStrategy : IDirectIOCommand
{
    public int CommandCode => DirectIOCommands.GetDiagnosticLog;

    public DirectIOData Execute(int data, object obj, SimulatorCashChanger device)
    {
        // 簡易的に CheckHealthText をログとして返却する実装
        return new DirectIOData(data, device.CheckHealthText);
    }
}
