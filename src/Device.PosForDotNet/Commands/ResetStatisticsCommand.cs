using CashChangerSimulator.Device.PosForDotNet.Coordination;

namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>統計情報リセット操作をカプセル化するコマンド。</summary>
/// <param name="statistics">リセット対象の統計情報名の配列。</param>
public class ResetStatisticsCommand(
    string[] statistics) : IUposCommand
{
    /// <inheritdoc/>
    public void Execute() => ExecuteAsync().GetAwaiter().GetResult();

    /// <inheritdoc/>
    public Task ExecuteAsync()
    {
        // Reset logic can be added if needed
        System.Diagnostics.Debug.WriteLine($"ResetStatisticsCommand: {string.Join(",", statistics)}");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Verify(IUposMediator mediator)
    {
        ArgumentNullException.ThrowIfNull(mediator);
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: true);
    }
}
