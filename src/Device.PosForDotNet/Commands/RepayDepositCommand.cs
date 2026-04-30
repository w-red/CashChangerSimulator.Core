using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device.PosForDotNet.Coordination;
using CashChangerSimulator.Device.Virtual;

namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>投入返却操作をカプセル化するコマンド。</summary>
/// <param name="controller">入金制御を司るコントローラー。</param>
public class RepayDepositCommand(
    DepositController controller)
    : IUposCommand
{
    /// <inheritdoc/>
    public void Execute()
    {
        ExecuteAsync().GetAwaiter().GetResult();
        if (controller.LastErrorCode != DeviceErrorCode.Success)
        {
            throw new DeviceException("RepayDeposit failed", controller.LastErrorCode, controller.LastErrorCodeExtended);
        }
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync()
    {
        await controller.RepayDepositAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Verify(IUposMediator mediator)
    {
        ArgumentNullException.ThrowIfNull(mediator);
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: false);
    }
}
