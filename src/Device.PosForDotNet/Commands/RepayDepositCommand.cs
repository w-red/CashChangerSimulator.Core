using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Device.PosForDotNet.Coordination;
using CashChangerSimulator.Device.Virtual;

namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>投入返却操作をカプセル化するコマンド。</summary>
public class RepayDepositCommand : IUposCommand
{
    private readonly DepositController _controller;
    private IUposMediator? _mediator;

    public RepayDepositCommand(DepositController controller)
    {
        _controller = controller;
    }

    public void Execute()
    {
        ExecuteAsync().GetAwaiter().GetResult();
        if (_controller.LastErrorCode != DeviceErrorCode.Success)
        {
            throw new DeviceException("RepayDeposit failed", _controller.LastErrorCode, _controller.LastErrorCodeExtended);
        }
    }

    public async Task ExecuteAsync()
    {
        await _controller.RepayDepositAsync();
    }

    public void Verify(IUposMediator mediator)
    {
        _mediator = mediator;
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: false);
    }
}
