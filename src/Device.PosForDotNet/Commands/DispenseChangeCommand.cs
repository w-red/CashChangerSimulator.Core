using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Device.PosForDotNet.Coordination;
using CashChangerSimulator.Device.Virtual;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>出金確定操作をカプセル化するコマンド。</summary>
public class DispenseChangeCommand : IUposCommand
{
    private readonly DispenseController controller;
    private readonly HardwareStatusManager hardwareStatusManager;
    private readonly DepositController depositController;
    private readonly decimal amount;
    private readonly bool isAsync;
    private IUposMediator? mediator;

    /// <summary>Initializes a new instance of the <see cref="DispenseChangeCommand"/> class.金額指定出金コマンドのインスタンスを初期化します。</summary>
    /// <param name="controller">出金制御を司るコントローラー。</param>
    /// <param name="hardwareStatusManager">ハードウェア状態を管理するマネージャー。</param>
    /// <param name="depositController">入金状態を確認するためのコントローラー。</param>
    /// <param name="amount">出金する金額。</param>
    /// <param name="asyncMode">非同期実行するかどうか。</param>
    public DispenseChangeCommand(
        DispenseController controller,
        HardwareStatusManager hardwareStatusManager,
        DepositController depositController,
        decimal amount,
        bool asyncMode)
    {
        this.controller = controller;
        this.hardwareStatusManager = hardwareStatusManager;
        this.depositController = depositController;
        this.amount = amount;
        isAsync = asyncMode;
    }

    /// <summary>金額指定出金操作を実行します。</summary>
    public void Execute()
    {
        if (isAsync)
        {
            // In asynchronous mode, we must return control immediately.
            // The task will run in the background.
            _ = ExecuteAsync();
        }
        else
        {
            ExecuteAsync().GetAwaiter().GetResult();
            if (controller.LastErrorCode != DeviceErrorCode.Success)
            {
                throw new DeviceException("DispenseChange failed", controller.LastErrorCode, controller.LastErrorCodeExtended);
            }
        }
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync()
    {
        if (isAsync && mediator != null)
        {
            mediator.IsBusy = true;
        }

        try
        {
            var amountAsInt = (int)amount;
            await controller.DispenseChangeAsync(amountAsInt, false).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] DispenseChangeCommand.ExecuteAsync failed: {ex.Message}");
            throw;
        }
        finally
        {
            if (isAsync && mediator != null)
            {
                mediator.IsBusy = false;
            }
        }
    }

    /// <summary>コマンド実行前の状態および事前条件(ハードウェア状態)を検証します。</summary>
    /// <param name="mediator">検証に使用するメディエーター。</param>
    public void Verify(IUposMediator mediator)
    {
        this.mediator = mediator;
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: true, mustNotBeBusy: true);

        // Pre-condition checks previously in Facade
        if (hardwareStatusManager.IsJammed.CurrentValue)
        {
            throw new PosControlException(
                "Device is jammed. Cannot dispense.",
                ErrorCode.Extended,
                (int)UposCashChangerErrorCodeExtended.Jam);
        }

        if (depositController.IsDepositInProgress)
        {
            throw new PosControlException(
                "Cash cannot be dispensed because cash acceptance is in progress.",
                ErrorCode.Illegal);
        }
    }
}
