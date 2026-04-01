using CashChangerSimulator.Device.Coordination;

namespace CashChangerSimulator.Device.Commands;

/// <summary>統計情報リセット操作をカプセル化するコマンド。</summary>
public class ResetStatisticsCommand : IUposCommand
{
    private readonly string[] _statistics;

    public ResetStatisticsCommand(string[] statistics)
    {
        _statistics = statistics;
    }

    public void Execute()
    {
        // Reset logic can be added if needed
    }

    public void Verify(IUposMediator mediator)
    {
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: true);
    }
}
