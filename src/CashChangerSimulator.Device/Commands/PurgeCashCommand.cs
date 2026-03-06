using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Device.Coordination;

namespace CashChangerSimulator.Device.Commands;

/// <summary>在庫回収操作をカプセル化するコマンド。</summary>
public class PurgeCashCommand : IUposCommand
{
    private readonly CashChangerManager _manager;

    public PurgeCashCommand(CashChangerManager manager)
    {
        _manager = manager;
    }

    public void Execute() => _manager.PurgeCash();

    public void Verify(IUposMediator mediator, bool skipStateVerification)
    {
        mediator.VerifyState(skipStateVerification, mustBeClaimed: true, mustBeEnabled: true, mustNotBeBusy: true);
    }
}
