using CashChangerSimulator.Device.PosForDotNet.Coordination;
using CashChangerSimulator.Device.Virtual;

using CashChangerSimulator.Core.Models;
namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>出金キャンセル操作をカプセル化するコマンド。</summary>
public class ClearOutputCommand(DispenseController controller) : IUposCommand
{
    private readonly DispenseController controller = controller;

    /// <inheritdoc/>
    public void Execute() => ExecuteAsync().GetAwaiter().GetResult();

    /// <inheritdoc/>
    public Task ExecuteAsync()
    {
        controller.ClearOutput();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Verify(IUposMediator mediator)
    {
        // ClearOutput can usually be called even if disabled or not claimed?
        // UPOS: Should be called if claimed.
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: false);
    }
}
