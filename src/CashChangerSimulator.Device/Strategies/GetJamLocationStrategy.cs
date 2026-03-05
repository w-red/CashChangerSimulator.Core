using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Core.Models;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.Strategies;

/// <summary>現在発生中のジャム箇所を取得する戦略。</summary>
public class GetJamLocationStrategy : IDirectIOCommand
{
    public int CommandCode => DirectIOCommands.GetJamLocation;

    public DirectIOData Execute(int data, object obj, SimulatorCashChanger device)
    {
        var location = device._hardwareStatusManager.JamLocation.Value.ToString();
        return new DirectIOData(data, location);
    }
}
