using CashChangerSimulator.Device.Virtual;
using CashChangerSimulator.Device.PosForDotNet.Coordination;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.PosForDotNet.Commands;

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

    public void Execute()
    {
        var coreAction = _action switch
        {
            CashDepositAction.Change => DepositAction.Store,
            CashDepositAction.NoChange => DepositAction.Repay,
            _ => DepositAction.Store
        };
        _controller.EndDeposit(coreAction);
    }

    public void Verify(IUposMediator mediator)
    {
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: false);
    }
}
