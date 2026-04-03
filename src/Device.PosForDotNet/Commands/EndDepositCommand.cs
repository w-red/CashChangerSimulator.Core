using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Device.Virtual;
using CashChangerSimulator.Device.PosForDotNet.Coordination;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>投入終了操作をカプセル化するコマンド。</summary>
public class EndDepositCommand : IUposCommand
{
    private readonly DepositController _controller;
    private readonly CashDepositAction _action;
    private IUposMediator? _mediator;

    public EndDepositCommand(DepositController controller, CashDepositAction action)
    {
        _controller = controller;
        _action = action;
    }

    public void Execute()
    {
        ExecuteAsync().GetAwaiter().GetResult();
        if (_controller.LastErrorCode != DeviceErrorCode.Success)
        {
            throw new DeviceException("EndDeposit failed", _controller.LastErrorCode, _controller.LastErrorCodeExtended);
        }
    }

    public async Task ExecuteAsync()
    {
        var actionText = _action.ToString();
        var actionValue = (int)_action;

        var coreAction = actionText switch
        {
            "Change" => DepositAction.Store,
            "NoChange" => DepositAction.Store,
            "Repay" => DepositAction.Repay,
            _ when actionValue == 3 || actionValue == 4 => DepositAction.Repay,
            _ => DepositAction.Store
        };
        await _controller.EndDepositAsync(coreAction);
    }

    public void Verify(IUposMediator mediator)
    {
        _mediator = mediator;
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: false);
    }
}
