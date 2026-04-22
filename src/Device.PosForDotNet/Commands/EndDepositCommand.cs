using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device.PosForDotNet.Coordination;
using CashChangerSimulator.Device.Virtual;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>投入終了操作をカプセル化するコマンド。</summary>
/// <param name="controller">入金制御を司るコントローラー。</param>
/// <param name="action">終了時のアクション。</param>
public class EndDepositCommand(DepositController controller, CashDepositAction action) : IUposCommand
{
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
            "Change" => DepositAction.Change,
            "NoChange" => DepositAction.NoChange,
            "Store" => DepositAction.NoChange, // Backward compatibility or synonym
            "Repay" => DepositAction.Repay,
            _ when actionValue == 3 || actionValue == 4 => DepositAction.Repay,
            _ => DepositAction.NoChange
        };
        await controller.EndDepositAsync(coreAction).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Verify(IUposMediator mediator)
    {
        ArgumentNullException.ThrowIfNull(mediator);
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: false);
    }
}
