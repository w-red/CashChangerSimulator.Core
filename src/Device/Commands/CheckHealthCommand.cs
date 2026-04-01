using CashChangerSimulator.Device.Coordination;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.Commands;

/// <summary>ヘルスチェック操作をカプセル化するコマンド。</summary>
public class CheckHealthCommand : IUposCommand
{
    private readonly DiagnosticController _controller;
    private readonly HealthCheckLevel _level;

    public CheckHealthCommand(DiagnosticController controller, HealthCheckLevel level)
    {
        _controller = controller;
        _level = level;
        Result = string.Empty;
    }

    public string Result { get; private set; }

    public void Execute()
    {
        Result = _controller.GetHealthReport(_level);
    }

    public void Verify(IUposMediator mediator)
    {
        // CheckHealth doesn't require open/claim/enable
    }
}
