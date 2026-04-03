using CashChangerSimulator.Device.Virtual;
using CashChangerSimulator.Device.PosForDotNet.Coordination;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.PosForDotNet.Commands;

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

    public void Execute() => ExecuteAsync().GetAwaiter().GetResult();

    public Task ExecuteAsync()
    {
        Result = _controller.GetHealthReport((DeviceHealthCheckLevel)_level);
        return Task.CompletedTask;
    }

    public void Verify(IUposMediator mediator)
    {
        // CheckHealth doesn't require open/claim/enable
    }
}
