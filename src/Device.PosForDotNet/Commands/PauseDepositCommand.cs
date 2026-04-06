using CashChangerSimulator.Device.PosForDotNet.Coordination;
using CashChangerSimulator.Device.Virtual;
using Microsoft.PointOfService;
using CashChangerSimulator.Core.Models;

namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>投入一時停止操作をカプセル化するコマンド。</summary>
public class PauseDepositCommand(DepositController controller, CashDepositPause pause) : IUposCommand
{
    private readonly DepositController controller = controller;
    private readonly CashDepositPause pause = pause;

    /// <inheritdoc/>
    public void Execute() => ExecuteAsync().GetAwaiter().GetResult();

    /// <inheritdoc/>
    public Task ExecuteAsync()
    {
        controller.PauseDeposit(pause == CashDepositPause.Pause ? DeviceDepositPause.Pause : DeviceDepositPause.Resume);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Verify(IUposMediator mediator)
    {
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: false);
    }
}
