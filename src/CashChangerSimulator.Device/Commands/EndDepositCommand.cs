using CashChangerSimulator.Device.Coordination;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.Commands;

/// <summary>投入終了操作をカプセル化するコマンド。</summary>
public class EndDepositCommand : IUposCommand
{
    private readonly DepositController _controller;
    private readonly CashDepositAction _action;

    public EndDepositCommand(DepositController controller, CashDepositAction action)
    {
        _controller = controller;
        _action = action;
    }

    public void Execute() => _controller.EndDeposit(_action);

    public void Verify(IUposMediator mediator)
    {
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: false);
    }
}
