using CashChangerSimulator.Device.PosForDotNet.Coordination;
using CashChangerSimulator.Device.Virtual;

namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>投入開始操作をカプセル化するコマンド。</summary>
public class BeginDepositCommand : IUposCommand
{
    private readonly DepositController _controller;

    public BeginDepositCommand(DepositController controller)
    {
        _controller = controller;
    }

    public void Execute() => ExecuteAsync().GetAwaiter().GetResult();

    public Task ExecuteAsync()
    {
        _controller.BeginDeposit();
        return Task.CompletedTask;
    }

    public void Verify(IUposMediator mediator)
    {
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: false);
    }
}
