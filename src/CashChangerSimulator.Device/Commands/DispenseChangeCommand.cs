using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Device.Coordination;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.Commands;

/// <summary>出金確定操作をカプセル化するコマンド。</summary>
public class DispenseChangeCommand : IUposCommand
{
    private readonly DispenseController _controller;
    private readonly HardwareStatusManager _hardwareStatusManager;
    private readonly DepositController _depositController;
    private readonly decimal _amount;
    private readonly bool _async;
    private readonly Action<ErrorCode, int> _onComplete;

    /// <summary>金額指定出金コマンドのインスタンスを初期化します。</summary>
    /// <param name="controller">出金制御を司るコントローラー。</param>
    /// <param name="hardwareStatusManager">ハードウェア状態を管理するマネージャー。</param>
    /// <param name="depositController">入金状態を確認するためのコントローラー。</param>
    /// <param name="amount">出金する合計金額。</param>
    /// <param name="async">非同期実行するかどうか。</param>
    /// <param name="onComplete">完了時に実行されるコールバック。</param>
    public DispenseChangeCommand(
        DispenseController controller, 
        HardwareStatusManager hardwareStatusManager,
        DepositController depositController,
        decimal amount, 
        bool async, 
        Action<ErrorCode, int> onComplete)
    {
        _controller = controller;
        _hardwareStatusManager = hardwareStatusManager;
        _depositController = depositController;
        _amount = amount;
        _async = async;
        _onComplete = onComplete;
    }

    /// <summary>金額指定出金操作を実行します。</summary>
    public void Execute()
    {
        var task = _controller.DispenseChangeAsync(_amount, _async, _onComplete);
        if (!_async)
        {
            task.GetAwaiter().GetResult();
        }
    }

    /// <summary>コマンド実行前の状態および事前条件（ハードウェア状態）を検証します。</summary>
    /// <param name="mediator">検証に使用するメディエーター。</param>
    public void Verify(IUposMediator mediator)
    {
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: true, mustNotBeBusy: true);

        // Pre-condition checks previously in Facade
        if (_hardwareStatusManager.IsJammed.Value)
            throw new PosControlException(
                "Device is jammed. Cannot dispense.",
                ErrorCode.Extended,
                (int)UposCashChangerErrorCodeExtended.Jam);
                
        if (_depositController.IsDepositInProgress)
            throw new PosControlException(
                "Cash cannot be dispensed because cash acceptance is in progress.",
                ErrorCode.Illegal);
    }
}
