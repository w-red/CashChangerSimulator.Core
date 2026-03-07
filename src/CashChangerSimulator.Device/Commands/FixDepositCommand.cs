using CashChangerSimulator.Device.Coordination;

namespace CashChangerSimulator.Device.Commands;

/// <summary>投入確定操作をカプセル化するコマンド。</summary>
public class FixDepositCommand : IUposCommand
{
    private readonly DepositController _controller;

    public FixDepositCommand(DepositController controller)
    {
        _controller = controller;
    }

    public void Execute() => _controller.FixDeposit();

    public void Verify(IUposMediator mediator)
    {
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: false);
    }
}
