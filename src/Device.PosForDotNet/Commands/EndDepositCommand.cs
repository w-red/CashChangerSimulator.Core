using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Device.PosForDotNet.Coordination;
using CashChangerSimulator.Device.Virtual;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>投入終了操作をカプセル化するコマンド。.</summary>
public class EndDepositCommand : IUposCommand
{
    private readonly DepositController controller;
    private readonly CashDepositAction action;
    private IUposMediator? mediator;

    /// <inheritdoc/>
    public EndDepositCommand(DepositController controller, CashDepositAction action)
    {
        this.controller = controller;
        this.action = action;
    }

    /// <inheritdoc/>
    public void Execute()
    {
        ExecuteAsync().GetAwaiter().GetResult();
        if (controller.LastErrorCode != DeviceErrorCode.Success)
        {
            throw new DeviceException("EndDeposit failed", controller.LastErrorCode, controller.LastErrorCodeExtended);
        }
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync()
    {
        var actionText = action.ToString();
        var actionValue = (int)action;

        var coreAction = actionText switch
        {
            "Change" => DepositAction.Store,
            "NoChange" => DepositAction.Store,
            "Repay" => DepositAction.Repay,
            _ when actionValue == 3 || actionValue == 4 => DepositAction.Repay,
            _ => DepositAction.Store
        };
        await controller.EndDepositAsync(coreAction).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Verify(IUposMediator mediator)
    {
        this.mediator = mediator;
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: false);
    }
}
