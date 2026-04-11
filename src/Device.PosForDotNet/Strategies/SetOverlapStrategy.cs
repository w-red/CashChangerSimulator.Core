using CashChangerSimulator.Core.Opos;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.PosForDotNet.Strategies;

/// <summary>重複投入(Overlap)エラー状態を設定または解除する戦略。</summary>
public class SetOverlapStrategy : IDirectIOCommand
{
    /// <inheritdoc/>
    public int CommandCode => DirectIOCommands.SetOverlap;

    /// <inheritdoc/>
    public DirectIOData Execute(int data, object obj, SimulatorCashChanger device)
    {
        device.HardwareStatusManager.Input.IsOverlapped.Value = data != 0;
        return new DirectIOData(data, obj);
    }
}
