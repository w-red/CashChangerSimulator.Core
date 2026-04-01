using CashChangerSimulator.Device.Virtual;
using CashChangerSimulator.Device.PosForDotNet.Coordination;

namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>統計情報取得操作をカプセル化するコマンド。</summary>
public class RetrieveStatisticsCommand : IUposCommand
{
    private readonly DiagnosticController _controller;
    private readonly string[] _statistics;

    public RetrieveStatisticsCommand(DiagnosticController controller, string[] statistics)
    {
        _controller = controller;
        _statistics = statistics;
        Result = string.Empty;
    }

    public string Result { get; private set; }

    public void Execute()
    {
        Result = _controller.RetrieveStatistics(_statistics);
    }

    public void Verify(IUposMediator mediator)
    {
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: true);
    }
}
