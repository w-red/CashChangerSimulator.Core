using CashChangerSimulator.Device.Coordination;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.Commands;

/// <summary>統計情報更新操作をカプセル化するコマンド。</summary>
public class UpdateStatisticsCommand : IUposCommand
{
    private readonly Statistic[] _statistics;

    public UpdateStatisticsCommand(Statistic[] statistics)
    {
        _statistics = statistics;
    }

    public void Execute()
    {
        // Simulator doesn't support external update, but follows UPOS protocol
    }

    public void Verify(IUposMediator mediator)
    {
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: true);
    }
}
