using CashChangerSimulator.Device.PosForDotNet.Coordination;

namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>統計情報リセット操作をカプセル化するコマンド。</summary>
public class ResetStatisticsCommand(string[] statistics) : IUposCommand
{
    private readonly string[] statistics = statistics;

    /// <inheritdoc/>
    public void Execute() => ExecuteAsync().GetAwaiter().GetResult();

    /// <inheritdoc/>
    public Task ExecuteAsync()
    {
        // Reset logic can be added if needed
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Verify(IUposMediator mediator)
    {
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: true);
    }
}
