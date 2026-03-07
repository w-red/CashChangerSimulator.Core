using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device.Coordination;
using Microsoft.PointOfService;
using System;
using System.Collections.Generic;

namespace CashChangerSimulator.Device.Commands;

/// <summary>金種指定出金操作をカプセル化するコマンド。</summary>
public class DispenseCashCommand : IUposCommand
{
    private readonly DispenseController _controller;
    private readonly IReadOnlyDictionary<DenominationKey, int> _counts;
    private readonly bool _async;
    private readonly Action<ErrorCode, int> _onComplete;

    public DispenseCashCommand(DispenseController controller, IReadOnlyDictionary<DenominationKey, int> counts, bool async, Action<ErrorCode, int> onComplete)
    {
        _controller = controller;
        _counts = counts;
        _async = async;
        _onComplete = onComplete;
    }

    public void Execute()
    {
        var task = _controller.DispenseCashAsync(_counts, _async, _onComplete);
        if (!_async)
        {
            task.GetAwaiter().GetResult();
        }
    }

    public void Verify(IUposMediator mediator)
    {
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: true, mustNotBeBusy: true);
    }
}
