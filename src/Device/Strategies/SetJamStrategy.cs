using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Core.Models;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.Strategies;

/// <summary>メカニカルジャム（Jam）エラー状態を設定または解除する戦略。</summary>
public class SetJamStrategy : IDirectIOCommand
{
    public int CommandCode => DirectIOCommands.SetJam;

    public DirectIOData Execute(int data, object obj, SimulatorCashChanger device)
    {
        var jam = data != 0;
        var location = JamLocation.None;

        if (jam && obj is string locationStr && !string.IsNullOrEmpty(locationStr))
        {
            if (Enum.TryParse<JamLocation>(locationStr, true, out var parsedLocation))
            {
                location = parsedLocation;
            }
        }

        device.HardwareStatusManager.SetJammed(jam, location);
        return new DirectIOData(data, obj);
    }
}
