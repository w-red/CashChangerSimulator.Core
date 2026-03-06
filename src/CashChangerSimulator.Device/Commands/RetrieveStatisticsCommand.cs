using CashChangerSimulator.Device.Coordination;

namespace CashChangerSimulator.Device.Commands;

/// <summary>統計情報取得操作をカプセル化するコマンド。</summary>
public class RetrieveStatisticsCommand : IUposCommand
{
    private readonly DiagnosticController _controller;
    private readonly string[] _statistics;

    public RetrieveStatisticsCommand(DiagnosticController controller, string[] statistics)
    {
        _controller = controller;
        _statistics = statistics;
    }

    public string Result { get; private set; }

    public void Execute()
    {
        Result = _controller.RetrieveStatistics(_statistics);
    }

    public void Verify(IUposMediator mediator, bool skipStateVerification)
    {
        mediator.VerifyState(skipStateVerification, mustBeClaimed: true, mustBeEnabled: true);
    }
}
