using CashChangerSimulator.Device.Coordination;

namespace CashChangerSimulator.Device.Commands;

/// <summary>出金キャンセル操作をカプセル化するコマンド。</summary>
public class ClearOutputCommand : IUposCommand
{
    private readonly DispenseController _controller;

    public ClearOutputCommand(DispenseController controller)
    {
        _controller = controller;
    }

    public void Execute() => _controller.ClearOutput();

    public void Verify(IUposMediator mediator)
    {
        // ClearOutput can usually be called even if disabled or not claimed? 
        // UPOS: Should be called if claimed.
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: false);
    }
}
