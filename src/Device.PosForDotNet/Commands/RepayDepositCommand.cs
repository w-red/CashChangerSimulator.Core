using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Device.PosForDotNet.Coordination;
using CashChangerSimulator.Device.Virtual;

namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>投入返却操作をカプセル化するコマンド。</summary>
public class RepayDepositCommand : IUposCommand
{
    private readonly DepositController controller;
    private IUposMediator? mediator;

    /// <summary><see cref="RepayDepositCommand"/> クラスの新しいインスタンスを初期化します。</summary>
    /// <param name="controller">入金コントローラー。</param>
    public RepayDepositCommand(DepositController controller)
    {
        this.controller = controller;
    }

    /// <summary>コマンドを実行します。</summary>
    public void Execute()
    {
        ExecuteAsync().GetAwaiter().GetResult();
        if (controller.LastErrorCode != DeviceErrorCode.Success)
        {
            throw new DeviceException("RepayDeposit failed", controller.LastErrorCode, controller.LastErrorCodeExtended);
        }
    }

    /// <summary>コマンドを非同期で実行します。</summary>
    public async Task ExecuteAsync()
    {
        await controller.RepayDepositAsync().ConfigureAwait(false);
    }

    /// <summary>コマンドの実行条件を検証します。</summary>
    public void Verify(IUposMediator mediator)
    {
        this.mediator = mediator;
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: false);
    }
}
