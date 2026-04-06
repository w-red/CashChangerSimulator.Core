using CashChangerSimulator.Device.PosForDotNet.Coordination;
using CashChangerSimulator.Device.Virtual;

namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>出金キャンセル操作をカプセル化するコマンド。</summary>
public class ClearOutputCommand : IUposCommand
{
    private readonly DispenseController controller;

    /// <summary><see cref="ClearOutputCommand"/> クラスの新しいインスタンスを初期化します。</summary>
    /// <param name="controller">出金コントローラー。</param>
    public ClearOutputCommand(DispenseController controller)
    {
        this.controller = controller;
    }

    /// <summary>コマンドを実行します。</summary>
    public void Execute() => ExecuteAsync().GetAwaiter().GetResult();

    /// <summary>コマンドを非同期で実行します。</summary>
    public Task ExecuteAsync()
    {
        controller.ClearOutput();
        return Task.CompletedTask;
    }

    /// <summary>コマンドの実行条件を検証します。</summary>
    public void Verify(IUposMediator mediator)
    {
        // ClearOutput can usually be called even if disabled or not claimed?
        // UPOS: Should be called if claimed.
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: false);
    }
}
