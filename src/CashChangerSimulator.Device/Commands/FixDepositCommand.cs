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

    /// <summary>投入確定コマンドのインスタンスを初期化します。</summary>
    /// <param name="controller">投入制御を司るコントローラー。</param>
    public FixDepositCommand(DepositController controller)
    {
        _controller = controller;
    }

    /// <summary>投入確定操作を実行します。</summary>
    /// <remarks>
    /// RealTimeData無効時にバッファリングされた通知を再現するため、
    /// コントローラーの状態変化を監視し、必要に応じて DataEvent を発行します。
    /// </remarks>
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

    /// <summary>コマンド実行前の状態を検証します。</summary>
    /// <param name="mediator">検証に使用するメディエーター。</param>
    public void Verify(IUposMediator mediator)
    {
        _mediator = mediator;
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: false);
    }
}
