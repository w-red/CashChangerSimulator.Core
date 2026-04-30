using CashChangerSimulator.Device.PosForDotNet.Coordination;
using CashChangerSimulator.Device.Virtual;
namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>統計情報取得操作をカプセル化するコマンド。</summary>
public class RetrieveStatisticsCommand(
    DiagnosticController controller,
    string[] statistics)
    : IUposCommand
{
    private readonly DiagnosticController controller = controller;
    private readonly string[] statistics = statistics;

    /// <summary>診断の実行結果を取得します。</summary>
    public string Result { get; private set; } = string.Empty;

    /// <inheritdoc/>
    public void Execute() => ExecuteAsync().GetAwaiter().GetResult();

    /// <inheritdoc/>
    public Task ExecuteAsync()
    {
        Result = controller.RetrieveStatistics(statistics);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Verify(IUposMediator mediator)
    {
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: true);
    }
}
