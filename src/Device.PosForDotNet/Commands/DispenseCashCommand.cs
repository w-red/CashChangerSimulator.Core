using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Device.PosForDotNet.Coordination;
using CashChangerSimulator.Device.Virtual;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>金種指定出金操作をカプセル化するコマンド。</summary>
public class DispenseCashCommand : IUposCommand
{
    private readonly DispenseController controller;
    private readonly Inventory inventory;
    private readonly HardwareStatusManager hardwareStatusManager;
    private readonly DepositController depositController;
    private readonly IReadOnlyDictionary<DenominationKey, int> counts;
    private readonly bool async;
    private IUposMediator? mediator;

    /// <summary>Initializes a new instance of the <see cref="DispenseCashCommand"/> class.金種指定出金コマンドのインスタンスを初期化します。</summary>
    /// <param name="controller">出金制御を司るコントローラー。</param>
    /// <param name="inventory">在庫情報を管理するインベントリ。</param>
    /// <param name="hardwareStatusManager">ハードウェア状態を管理するマネージャー。</param>
    /// <param name="depositController">入金状態を確認するためのコントローラー。</param>
    /// <param name="counts">出金する金種と枚数のセット。</param>
    /// <param name="async">非同期実行するかどうか。</param>
    public DispenseCashCommand(
        DispenseController controller,
        Inventory inventory,
        HardwareStatusManager hardwareStatusManager,
        DepositController depositController,
        IReadOnlyDictionary<DenominationKey, int> counts,
        bool async)
    {
        this.controller = controller;
        this.inventory = inventory;
        this.hardwareStatusManager = hardwareStatusManager;
        this.depositController = depositController;
        this.counts = counts;
        this.async = async;
    }

    /// <summary>金種指定出金操作を実行します。</summary>
    public void Execute()
    {
        ExecuteAsync().GetAwaiter().GetResult();
        if (!async && controller.LastErrorCode != DeviceErrorCode.Success)
        {
            throw new DeviceException("DispenseCash failed", controller.LastErrorCode, controller.LastErrorCodeExtended);
        }
    }

    /// <summary>金種指定出金操作を非同期で実行します。</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task ExecuteAsync()
    {
        if (async && mediator != null)
        {
            mediator.IsBusy = true;
        }

        await controller.DispenseCashAsync(counts, async).ConfigureAwait(false);
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
