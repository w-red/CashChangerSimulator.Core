using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Device.PosForDotNet.Coordination;
using CashChangerSimulator.Device.Virtual;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>金種指定出金操作をカプセル化するコマンド。</summary>
public class DispenseCashCommand(
    DispenseController controller,
    Inventory inventory,
    HardwareStatusManager hardwareStatusManager,
    DepositController depositController,
    IReadOnlyDictionary<DenominationKey, int> counts,
    bool isAsync)
    : IUposCommand
{
    private IUposMediator? mediator;

    /// <summary>金種指定出金操作を実行します。</summary>
    public void Execute()
    {
        ExecuteAsync().GetAwaiter().GetResult();
        if (!isAsync && controller.LastErrorCode != DeviceErrorCode.Success)
        {
            throw new DeviceException("DispenseCash failed", controller.LastErrorCode, controller.LastErrorCodeExtended);
        }
    }

    /// <summary>金種指定出金操作を非同期で実行します。</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task ExecuteAsync()
    {
        if (isAsync && mediator != null)
        {
            mediator.IsBusy = true;
        }

        await controller.DispenseCashAsync(counts, isAsync).ConfigureAwait(false);
    }

    /// <summary>コマンド実行前の状態および事前条件(在庫やハードウェア状態)を検証します。</summary>
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

        // Inventory check previously in Facade
        foreach (var (key, count) in counts)
        {
            if (!inventory.AllCounts.Any(kv => kv.Key == key))
            {
                throw new PosControlException(
                    $"Denomination {key} is not registered.",
                    ErrorCode.Illegal);
            }

            if (inventory.GetCount(key) < count)
            {
                throw new PosControlException(
                    $"Insufficient inventory for {key}. Required: {count}, Available: {inventory.GetCount(key)}",
                    ErrorCode.Extended,
                    (int)UposCashChangerErrorCodeExtended.OverDispense);
            }
        }
    }
}
