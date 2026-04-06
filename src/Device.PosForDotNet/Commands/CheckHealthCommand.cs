using CashChangerSimulator.Device.PosForDotNet.Coordination;
using CashChangerSimulator.Device.Virtual;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>ヘルスチェック操作をカプセル化するコマンド。</summary>
public class CheckHealthCommand : IUposCommand
{
    private readonly DiagnosticController controller;
    private readonly HealthCheckLevel level;

    /// <summary><see cref="CheckHealthCommand"/> クラスの新しいインスタンスを初期化します。</summary>
    /// <param name="controller">診断コントローラー。</param>
    /// <param name="level">ヘルスチェックのレベル。</param>
    public CheckHealthCommand(DiagnosticController controller, HealthCheckLevel level)
    {
        this.controller = controller;
        this.level = level;
        Result = string.Empty;
    }

    /// <summary>診断の実行結果を取得します。</summary>
    public string Result { get; private set; }

    /// <inheritdoc/>
    public void Execute() => ExecuteAsync().GetAwaiter().GetResult();

    /// <inheritdoc/>
    public Task ExecuteAsync()
    {
        Result = controller.GetHealthReport((DeviceHealthCheckLevel)level);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Verify(IUposMediator mediator)
    {
        // CheckHealth doesn't require open/claim/enable
    }
}
