using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device.PosForDotNet.Coordination;
using CashChangerSimulator.Device.Virtual;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>ヘルスチェック操作をカプセル化するコマンド。</summary>
public class CheckHealthCommand(DiagnosticController controller, HealthCheckLevel level) : IUposCommand
{
    private readonly DiagnosticController controller = controller;
    private readonly HealthCheckLevel level = level;

    /// <summary>診断の実行結果を取得します。</summary>
    public string Result { get; private set; } = string.Empty;

    /// <inheritdoc/>
    public void Execute() => ExecuteAsync().GetAwaiter().GetResult();

    /// <inheritdoc/>
    public Task ExecuteAsync()
    {
        Result = controller.GetHealthReport((PosSharp.Abstractions.HealthCheckLevel)level);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Verify(IUposMediator mediator)
    {
        // CheckHealth doesn't require open/claim/enable
    }
}
