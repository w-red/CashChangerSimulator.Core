using CashChangerSimulator.Device.PosForDotNet.Coordination;
using Microsoft.PointOfService;
namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>統計情報更新操作をカプセル化するコマンド。</summary>
/// <param name="statistics">更新する統計情報の配列。</param>
public class UpdateStatisticsCommand(
    Statistic[] statistics) : IUposCommand
{
    /// <inheritdoc/>
    public void Execute() => ExecuteAsync().GetAwaiter().GetResult();

    /// <inheritdoc/>
    public Task ExecuteAsync()
    {
        // Simulator doesn't support external update, but follows UPOS protocol
        System.Diagnostics.Debug.WriteLine($"UpdateStatisticsCommand: {statistics.Length} statistics provided");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Verify(IUposMediator mediator)
    {
        ArgumentNullException.ThrowIfNull(mediator);
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: true);
    }
}
