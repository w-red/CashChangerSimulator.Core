using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Device.PosForDotNet.Coordination;

namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>在庫回収操作をカプセル化するコマンド。.</summary>
public class PurgeCashCommand : IUposCommand
{
    private readonly CashChangerManager manager;

    /// <inheritdoc/>
    public PurgeCashCommand(CashChangerManager manager)
    {
        this.manager = manager;
    }

    /// <inheritdoc/>
    public void Execute() => ExecuteAsync().GetAwaiter().GetResult();

    /// <inheritdoc/>
    public Task ExecuteAsync()
    {
        manager.PurgeCash();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Verify(IUposMediator mediator)
    {
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: true, mustNotBeBusy: true);
    }
}
