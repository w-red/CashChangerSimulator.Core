using CashChangerSimulator.Device.Virtual;
using CashChangerSimulator.Device.PosForDotNet.Coordination;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>投入一時停止操作をカプセル化するコマンド。</summary>
public class PauseDepositCommand : IUposCommand
{
    private readonly DepositController _controller;
    private readonly CashDepositPause _pause;

    public PauseDepositCommand(DepositController controller, CashDepositPause pause)
    {
        _controller = controller;
        _pause = pause;
    }

    public void Execute()
    {
        _controller.PauseDeposit(_pause == CashDepositPause.Pause ? DeviceDepositPause.Pause : DeviceDepositPause.Resume);
    }

    public void Verify(IUposMediator mediator)
    {
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: false);
    }
}
