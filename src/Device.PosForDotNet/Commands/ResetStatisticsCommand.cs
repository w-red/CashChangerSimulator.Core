using CashChangerSimulator.Device.PosForDotNet.Coordination;

namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>統計情報リセット操作をカプセル化するコマンド。</summary>
public class ResetStatisticsCommand : IUposCommand
{
    private readonly string[] _statistics;

    public ResetStatisticsCommand(string[] statistics)
    {
        _statistics = statistics;
    }

    public void Execute() => ExecuteAsync().GetAwaiter().GetResult();

    public Task ExecuteAsync()
    {
        // Reset logic can be added if needed
        return Task.CompletedTask;
    }

    public void Verify(IUposMediator mediator)
    {
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: true);
    }
}
