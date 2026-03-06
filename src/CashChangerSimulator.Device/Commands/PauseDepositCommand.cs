using CashChangerSimulator.Device.Coordination;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.Commands;

/// <summary>投入一時停止操作をカプセル化するコマンド。</summary>
public class PauseDepositCommand : IUposCommand
{
    private readonly DepositController _controller;
    private readonly CashDepositPause _control;

    public PauseDepositCommand(DepositController controller, CashDepositPause control)
    {
        _controller = controller;
        _control = control;
    }

    public void Execute() => _controller.PauseDeposit(_control);

    public void Verify(IUposMediator mediator, bool skipStateVerification)
    {
        mediator.VerifyState(skipStateVerification, mustBeClaimed: true, mustBeEnabled: false);
    }
}
