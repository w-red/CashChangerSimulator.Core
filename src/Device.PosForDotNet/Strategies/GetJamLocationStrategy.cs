using CashChangerSimulator.Core.Opos;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.PosForDotNet.Strategies;

/// <summary>現在発生中のジャム箇所を取得する戦略。</summary>
public class GetJamLocationStrategy : IDirectIOCommand
{
    /// <inheritdoc/>
    public int CommandCode => DirectIOCommands.GetJamLocation;

    /// <inheritdoc/>
    public DirectIOData Execute(int data, object obj, SimulatorCashChanger device)
    {
        var location = device.HardwareStatusManager.JamLocation.Value.ToString();
        return new DirectIOData(data, location);
    }
}
