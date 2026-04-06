using CashChangerSimulator.Device.PosForDotNet.Coordination;
using CashChangerSimulator.Device.Virtual;

namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>統計情報取得操作をカプセル化するコマンド。</summary>
public class RetrieveStatisticsCommand : IUposCommand
{
    private readonly DiagnosticController controller;
    private readonly string[] statistics;

    /// <inheritdoc/>
    public RetrieveStatisticsCommand(DiagnosticController controller, string[] statistics)
    {
        this.controller = controller;
        this.statistics = statistics;
        Result = string.Empty;
    }

    /// <inheritdoc/>
    public string Result { get; private set; }

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
