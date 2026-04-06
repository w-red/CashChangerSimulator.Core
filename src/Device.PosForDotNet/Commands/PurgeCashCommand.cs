using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Device.PosForDotNet.Coordination;

namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>在庫回収操作をカプセル化するコマンド。</summary>
public class PurgeCashCommand : IUposCommand
{
    private readonly CashChangerManager manager;

    /// <summary><see cref="PurgeCashCommand"/> クラスの新しいインスタンスを初期化します。</summary>
    /// <param name="manager">現金管理マネージャー。</param>
    public PurgeCashCommand(CashChangerManager manager)
    {
        this.manager = manager;
    }

    /// <summary>コマンドを実行します。</summary>
    public void Execute() => ExecuteAsync().GetAwaiter().GetResult();

    /// <summary>コマンドを非同期で実行します。</summary>
    public Task ExecuteAsync()
    {
        manager.PurgeCash();
        return Task.CompletedTask;
    }

    /// <summary>コマンドの実行条件を検証します。</summary>
    public void Verify(IUposMediator mediator)
    {
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: true, mustNotBeBusy: true);
    }
}
