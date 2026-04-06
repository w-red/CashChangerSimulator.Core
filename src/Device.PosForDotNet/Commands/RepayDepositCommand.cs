using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Device.PosForDotNet.Coordination;
using CashChangerSimulator.Device.Virtual;

namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>投入返却操作をカプセル化するコマンド。.</summary>
public class RepayDepositCommand : IUposCommand
{
    private readonly DepositController controller;
    private IUposMediator? mediator;

    /// <inheritdoc/>
    public RepayDepositCommand(DepositController controller)
    {
        this.controller = controller;
    }

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
        this.mediator = mediator;
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: false);
    }
}
