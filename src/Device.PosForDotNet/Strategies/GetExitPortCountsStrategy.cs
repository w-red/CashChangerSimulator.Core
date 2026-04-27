using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Opos;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.PosForDotNet.Strategies;

/// <summary>出金口の払い出し枚数内訳を取得する DirectIO コマンド (201)。</summary>
public class GetExitPortCountsStrategy : IDirectIOCommand
{
    /// <inheritdoc/>
    public int CommandCode => DirectIOCommands.GetExitPortCounts;

    /// <inheritdoc/>
    public DirectIOData Execute(int data, object obj, SimulatorCashChanger device)
    {
        var port = (ExitPort)data;
        var counts = device.HardwareStatus.State.GetExitPortCounts(port);
        if (obj is IDictionary<DenominationKey, int> outDict)
        {
            outDict.Clear();
            foreach (var kv in counts)
            {
                outDict.Add(kv.Key, kv.Value);
            }
        }
        return new DirectIOData(0, obj);
    }
}
