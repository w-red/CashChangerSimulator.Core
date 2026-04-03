using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Device.PosForDotNet.Coordination;

namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>在庫回収操作をカプセル化するコマンド。</summary>
public class PurgeCashCommand : IUposCommand
{
    private readonly CashChangerManager _manager;

    public PurgeCashCommand(CashChangerManager manager)
    {
        _manager = manager;
    }

    public void Execute() => ExecuteAsync().GetAwaiter().GetResult();

    public Task ExecuteAsync()
    {
        _manager.PurgeCash();
        return Task.CompletedTask;
    }

    public void Verify(IUposMediator mediator)
    {
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: true, mustNotBeBusy: true);
    }
}
