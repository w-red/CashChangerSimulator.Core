using CashChangerSimulator.Device.PosForDotNet.Coordination;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>統計情報更新操作をカプセル化するコマンド。</summary>
public class UpdateStatisticsCommand : IUposCommand
{
    private readonly Statistic[] statistics;

    /// <inheritdoc/>
    public UpdateStatisticsCommand(Statistic[] statistics)
    {
        this.statistics = statistics;
    }

    /// <inheritdoc/>
    public void Execute() => ExecuteAsync().GetAwaiter().GetResult();

    /// <inheritdoc/>
    public Task ExecuteAsync()
    {
        // Simulator doesn't support external update, but follows UPOS protocol
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Verify(IUposMediator mediator)
    {
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: true);
    }
}
