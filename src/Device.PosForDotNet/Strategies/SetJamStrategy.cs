using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Opos;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.PosForDotNet.Strategies;

/// <summary>メカニカルジャム（Jam）エラー状態を設定または解除する戦略。</summary>
public class SetJamStrategy : IDirectIOCommand
{
    /// <inheritdoc/>
    public int CommandCode => DirectIOCommands.SetJam;

    /// <inheritdoc/>
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
