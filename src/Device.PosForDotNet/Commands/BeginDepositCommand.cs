using CashChangerSimulator.Device.PosForDotNet.Coordination;
using CashChangerSimulator.Device.Virtual;

namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>投入開始操作をカプセル化するコマンド。</summary>
public class BeginDepositCommand : IUposCommand
{
    private readonly DepositController controller;

    /// <inheritdoc/>
    public BeginDepositCommand(DepositController controller)
    {
        this.controller = controller;
    }

    /// <inheritdoc/>
    public void Execute() => ExecuteAsync().GetAwaiter().GetResult();

    /// <inheritdoc/>
    public Task ExecuteAsync()
    {
        controller.BeginDeposit();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Verify(IUposMediator mediator)
    {
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: false);
    }
}
