using CashChangerSimulator.Device.Strategies;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device;

/// <summary>
/// DirectIO コマンドの管理と実行を担当するクラス。
/// </summary>
public class DirectIOHandler
{
    private readonly Dictionary<int, IDirectIOCommand> _commands = [];

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
            new AdjustCashCountsStrStrategy(),
            new GetDepositedSerialsStrategy()
        };

        foreach (var strategy in strategies)
        {
            _commands[strategy.CommandCode] = strategy;
        }
    }

    /// <summary>
    /// 指定されたコマンドを実行します。
    /// </summary>
    public DirectIOData Handle(int command, int data, object obj, SimulatorCashChanger serviceObject)
    {
        return _commands.TryGetValue(command, out var strategy) ? strategy.Execute(data, obj, serviceObject) : new DirectIOData(data, obj);
    }
}
