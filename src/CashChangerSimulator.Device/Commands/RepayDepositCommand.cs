using CashChangerSimulator.Device.Coordination;

namespace CashChangerSimulator.Device.Commands;

/// <summary>投入返却操作をカプセル化するコマンド。</summary>
public class RepayDepositCommand : IUposCommand
{
    private readonly DepositController _controller;

    public RepayDepositCommand(DepositController controller)
    {
        _controller = controller;
    }

    public void Execute() => _controller.RepayDeposit();

    public void Verify(IUposMediator mediator)
    {
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: false);
    }
}
