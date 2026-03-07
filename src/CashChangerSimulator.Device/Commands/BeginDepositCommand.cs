using CashChangerSimulator.Device.Coordination;

namespace CashChangerSimulator.Device.Commands;

/// <summary>投入開始操作をカプセル化するコマンド。</summary>
public class BeginDepositCommand : IUposCommand
{
    private readonly DepositController _controller;

    public BeginDepositCommand(DepositController controller)
    {
        _controller = controller;
    }

    public void Execute()
    {
        _controller.BeginDeposit();
    }

    public void Verify(IUposMediator mediator)
    {
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: false);
    }
}
