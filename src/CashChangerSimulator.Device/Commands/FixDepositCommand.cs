using CashChangerSimulator.Device.Coordination;
using Microsoft.PointOfService;
using R3;
using System;

namespace CashChangerSimulator.Device.Commands;

/// <summary>投入確定操作をカプセル化するコマンド。</summary>
public class FixDepositCommand : IUposCommand
{
    private readonly DepositController _controller;

    private IUposMediator? _mediator;

    public FixDepositCommand(DepositController controller)
    {
        _controller = controller;
    }

    public void Execute()
    {
        // When RealTimeData is disabled, multiple DataEvents might be queued for buffered updates.
        // We observe the controller changes during FixDeposit to replicate legacy behavior.
        // Ensure _mediator is not null and capture it for closure safety.
        var mediator = _mediator;
        if (mediator == null)
        {
            _controller.FixDeposit();
            return;
        }

        using var subscription = mediator.EventSink.DepositChanged
            .Subscribe(_ => 
            {
                if (mediator.EventSink.DataEventEnabled)
                {
                    mediator.EventSink.NotifyEvent(new DataEventArgs(0));
                }
            });

        _controller.FixDeposit();
    }

    public void Verify(IUposMediator mediator)
    {
        _mediator = mediator;
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: false);
    }
}
