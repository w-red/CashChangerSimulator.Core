using CashChangerSimulator.Device.PosForDotNet.Strategies;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.PosForDotNet;

/// <summary>
/// DirectIO コマンドの管理と実行を担当するクラス。.
/// </summary>
public class DirectIOHandler
{
    private readonly Dictionary<int, IDirectIOCommand> commands = [];

    /// <inheritdoc/>
    public DirectIOHandler()
    {
        InitializeCommands();
    }

    private void InitializeCommands()
    {
        var strategies = new IDirectIOCommand[]
        {
            new SetOverlapStrategy(),
            new SetJamStrategy(),
            new SetDiscrepancyStrategy(),
            new SimulateRemovedStrategy(),
            new SimulateInsertedStrategy(),
            new GetVersionStrategy(),
            new GetJamLocationStrategy(),
            new AdjustCashCountsStrStrategy(),
            new GetDepositedSerialsStrategy(),
            new GetDiagnosticLogStrategy()
        };

        foreach (var strategy in strategies)
        {
            commands[strategy.CommandCode] = strategy;
        }
    }

    /// <summary>
    /// 指定されたコマンドを実行します。.
    /// </summary>
    /// <returns></returns>
    public DirectIOData Handle(int command, int data, object obj, SimulatorCashChanger serviceObject)
    {
        ArgumentNullException.ThrowIfNull(serviceObject);
        return commands.TryGetValue(command, out var strategy) ? strategy.Execute(data, obj, serviceObject) : new DirectIOData(data, obj);
    }
}
