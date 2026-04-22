using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Opos;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.PosForDotNet.Strategies;

/// <summary>メカニカルジャム(Jam)エラー状態を設定または解除する戦略。</summary>
public class SetJamStrategy : IDirectIOCommand
{
    /// <inheritdoc/>
    public int CommandCode => DirectIOCommands.SetJam;

    /// <inheritdoc/>
    public DirectIOData Execute(int data, object obj, SimulatorCashChanger device)
    {
        var jam = data != 0;
        var location = JamLocation.None;

        if (jam && obj is string locationStr && !string.IsNullOrEmpty(locationStr) &&
            Enum.TryParse<JamLocation>(locationStr, true, out var parsedLocation))
        {
            location = parsedLocation;
        }

        device.HardwareStatusManager.Input.IsJammed.Value = jam;
        device.HardwareStatusManager.Input.CurrentJamLocation.Value = location;
        return new DirectIOData(data, obj);
    }
}
