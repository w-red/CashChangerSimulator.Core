using CashChangerSimulator.Device.PosForDotNet.Coordination;
using CashChangerSimulator.Device.Virtual;
namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>投入確定操作をカプセル化するコマンド。</summary>
public class FixDepositCommand(DepositController controller) : IUposCommand
{
    /// <summary>投入確定操作を実行します。</summary>
    /// <remarks>
    /// RealTimeData無効時にバッファリングされた通知を再現するため、
    /// コントローラーの状態変化を監視し、必要に応じて DataEvent を発行します。
    /// </remarks>
    public void Execute() => ExecuteAsync().GetAwaiter().GetResult();

    /// <inheritdoc/>
    public Task ExecuteAsync()
    {
        controller.FixDeposit();
        return Task.CompletedTask;
    }

    /// <summary>コマンド実行前の状態を検証します。</summary>
    /// <param name="mediator">検証に使用するメディエーター。</param>
    public void Verify(IUposMediator mediator)
    {
        ArgumentNullException.ThrowIfNull(mediator);
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: false);
    }
}
