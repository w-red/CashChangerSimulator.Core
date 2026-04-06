using CashChangerSimulator.Device.PosForDotNet.Coordination;
using CashChangerSimulator.Device.Virtual;

namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>投入確定操作をカプセル化するコマンド。.</summary>
public class FixDepositCommand : IUposCommand
{
    private readonly DepositController controller;

    private IUposMediator? mediator;

    /// <summary>Initializes a new instance of the <see cref="FixDepositCommand"/> class.投入確定コマンドのインスタンスを初期化します。.</summary>
    /// <param name="controller">投入制御を司るコントローラー。.</param>
    public FixDepositCommand(DepositController controller)
    {
        this.controller = controller;
    }

    /// <summary>投入確定操作を実行します。.</summary>
    /// <remarks>
    /// RealTimeData無効時にバッファリングされた通知を再現するため、
    /// コントローラーの状態変化を監視し、必要に応じて DataEvent を発行します。.
    /// </remarks>
    public void Execute() => ExecuteAsync().GetAwaiter().GetResult();

    /// <inheritdoc/>
    public Task ExecuteAsync()
    {
        controller.FixDeposit();
        return Task.CompletedTask;
    }

    /// <summary>コマンド実行前の状態を検証します。.</summary>
    /// <param name="mediator">検証に使用するメディエーター。.</param>
    public void Verify(IUposMediator mediator)
    {
        this.mediator = mediator;
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: false);
    }
}
