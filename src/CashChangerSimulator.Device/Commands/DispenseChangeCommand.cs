using CashChangerSimulator.Device.Coordination;
using Microsoft.PointOfService;
using System;

namespace CashChangerSimulator.Device.Commands;

/// <summary>出金確定操作をカプセル化するコマンド。</summary>
public class DispenseChangeCommand : IUposCommand
{
    private readonly DispenseController _controller;
    private readonly decimal _amount;
    private readonly bool _async;
    private readonly Action<ErrorCode, int> _onComplete;

    public DispenseChangeCommand(DispenseController controller, decimal amount, bool async, Action<ErrorCode, int> onComplete)
    {
        _controller = controller;
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

    public void Verify(IUposMediator mediator, bool skipStateVerification)
    {
        mediator.VerifyState(skipStateVerification, mustBeClaimed: true, mustBeEnabled: true, mustNotBeBusy: true);
    }
}
