using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Device.Coordination;
using Microsoft.PointOfService;
using System;

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

    public void Execute()
    {
        var task = _controller.DispenseChangeAsync(_amount, _async, _onComplete);
        if (!_async)
        {
            task.GetAwaiter().GetResult();
        }
    }

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
