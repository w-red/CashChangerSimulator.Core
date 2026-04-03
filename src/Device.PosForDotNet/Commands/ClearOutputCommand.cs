using CashChangerSimulator.Device.PosForDotNet.Coordination;
using CashChangerSimulator.Device.Virtual;

namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>出金キャンセル操作をカプセル化するコマンド。</summary>
public class ClearOutputCommand : IUposCommand
{
    private readonly DispenseController _controller;

    public ClearOutputCommand(DispenseController controller)
    {
        _controller = controller;
    }

    public void Execute() => ExecuteAsync().GetAwaiter().GetResult();

    public Task ExecuteAsync()
    {
        _controller.ClearOutput();
        return Task.CompletedTask;
    }

    public void Verify(IUposMediator mediator)
    {
        // ClearOutput can usually be called even if disabled or not claimed? 
        // UPOS: Should be called if claimed.
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: false);
    }
}
