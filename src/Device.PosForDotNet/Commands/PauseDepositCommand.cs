using CashChangerSimulator.Device.PosForDotNet.Coordination;
using CashChangerSimulator.Device.Virtual;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>投入一時停止操作をカプセル化するコマンド。</summary>
public class PauseDepositCommand : IUposCommand
{
    private readonly DepositController controller;
    private readonly CashDepositPause pause;

    /// <summary><see cref="PauseDepositCommand"/> クラスの新しいインスタンスを初期化します。</summary>
    /// <param name="controller">入金コントローラー。</param>
    /// <param name="pause">一時停止の状態。</param>
    public PauseDepositCommand(DepositController controller, CashDepositPause pause)
    {
        this.controller = controller;
        this.pause = pause;
    }

    /// <summary>コマンドを実行します。</summary>
    public void Execute() => ExecuteAsync().GetAwaiter().GetResult();

    /// <summary>コマンドを非同期で実行します。</summary>
    public Task ExecuteAsync()
    {
        controller.PauseDeposit(pause == CashDepositPause.Pause ? DeviceDepositPause.Pause : DeviceDepositPause.Resume);
        return Task.CompletedTask;
    }

    /// <summary>コマンドの実行条件を検証します。</summary>
    public void Verify(IUposMediator mediator)
    {
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: false);
    }
}
