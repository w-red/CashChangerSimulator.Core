using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Device.PosForDotNet.Coordination;
using CashChangerSimulator.Device.Virtual;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>投入終了操作をカプセル化するコマンド。</summary>
public class EndDepositCommand : IUposCommand
{
    private readonly DepositController controller;
    private readonly CashDepositAction action;
    private IUposMediator? mediator;

    /// <summary><see cref="EndDepositCommand"/> クラスの新しいインスタンスを初期化します。</summary>
    /// <param name="controller">入金コントローラー。</param>
    /// <param name="action">入金完了時のアクション。</param>
    public EndDepositCommand(DepositController controller, CashDepositAction action)
    {
        this.controller = controller;
        this.action = action;
    }

    /// <summary>コマンドを実行します。</summary>
    public void Execute()
    {
        ExecuteAsync().GetAwaiter().GetResult();
        if (controller.LastErrorCode != DeviceErrorCode.Success)
        {
            throw new DeviceException("EndDeposit failed", controller.LastErrorCode, controller.LastErrorCodeExtended);
        }
    }

    /// <summary>コマンドを非同期で実行します。</summary>
    public async Task ExecuteAsync()
    {
        var actionText = action.ToString();
        var actionValue = (int)action;

        var coreAction = actionText switch
        {
            "Change" => DepositAction.Store,
            "NoChange" => DepositAction.Store,
            "Repay" => DepositAction.Repay,
            _ when actionValue == 3 || actionValue == 4 => DepositAction.Repay,
            _ => DepositAction.Store
        };
        await controller.EndDepositAsync(coreAction).ConfigureAwait(false);
    }

    /// <summary>コマンドの実行条件を検証します。</summary>
    public void Verify(IUposMediator mediator)
    {
        this.mediator = mediator;
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: false);
    }
}
