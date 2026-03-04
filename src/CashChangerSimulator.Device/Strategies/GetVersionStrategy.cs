using CashChangerSimulator.Core.Opos;
using Microsoft.PointOfService;
using System.Reflection;

namespace CashChangerSimulator.Device.Strategies;

/// <summary>シミュレーターのバージョン情報を取得する戦略。</summary>
public class GetVersionStrategy : IDirectIOCommand
{
    public int CommandCode => DirectIOCommands.GetVersion;

    public DirectIOData Execute(int data, object obj, SimulatorCashChanger device)
    {
        var version = $"SimulatorCashChanger v{Assembly.GetExecutingAssembly().GetName().Version}";
        return new DirectIOData(data, version);
    }
}
