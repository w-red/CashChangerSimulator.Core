using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device.PosForDotNet.Commands;
using CashChangerSimulator.Device.PosForDotNet.Coordination;
using CashChangerSimulator.Device.Virtual;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.PosForDotNet;

/// <summary>UPOS の出金操作を統合的に処理する Facade。</summary>
/// <remarks>
/// 金額指定の払い出し(DispenseChange)および金種指定の払い出し(DispenseCash)のリクエストを受け、
/// 適切なバリデーション(在庫確認、状態チェック)を行った後にコマンドを実行します。
/// </remarks>
public class UposDispenseFacade
{
    private readonly DispenseController dispenseController;
    private readonly DepositController depositController;
    private readonly HardwareStatusManager hardwareStatusManager;
    private readonly Inventory inventory;
    private readonly IUposMediator mediator;

    /// <summary>Initializes a new instance of the <see cref="UposDispenseFacade"/> class.必要なコントローラーとサービスを注入して Facade を初期化します。</summary>
    /// <param name="dispenseController">出金コントローラー。</param>
    /// <param name="depositController">入金コントローラー。</param>
    /// <param name="hardwareStatusManager">ハードウェアステータスマネージャー。</param>
    /// <param name="inventory">在庫管理オブジェクト。</param>
    /// <param name="mediator">UPOS メディエーター。</param>
    public UposDispenseFacade(
        DispenseController dispenseController,
        DepositController depositController,
        HardwareStatusManager hardwareStatusManager,
        Inventory inventory,
        IUposMediator mediator)
    {
        ArgumentNullException.ThrowIfNull(dispenseController);
        ArgumentNullException.ThrowIfNull(depositController);
        ArgumentNullException.ThrowIfNull(hardwareStatusManager);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(mediator);

        this.dispenseController = dispenseController;
        this.depositController = depositController;
        this.hardwareStatusManager = hardwareStatusManager;
        this.inventory = inventory;
        this.mediator = mediator;
    }

    /// <summary>金額指定の出金を実行します。</summary>
    /// <param name="amount">払出金額。</param>
    /// <param name="factor">金額の係数。</param>
    /// <param name="asyncMode">非同期実行フラグ。</param>
    public void DispenseByAmount(
        int amount,
        decimal factor,
        bool asyncMode)
    {
        if (amount <= 0)
        {
            throw new PosControlException("Amount must be positive", ErrorCode.Illegal);
        }

        var decimalAmount = amount / factor;
        var command = new DispenseChangeCommand(
            dispenseController,
            hardwareStatusManager,
            depositController,
            decimalAmount,
            asyncMode);

        mediator.Execute(command);
    }

    /// <summary>金種指定の出金を実行します。</summary>
    /// <param name="cashCounts">出金する金種ごとの数量。</param>
    /// <param name="currencyCode">通貨コード。</param>
    /// <param name="factor">金額の係数。</param>
    /// <param name="asyncMode">非同期実行フラグ。</param>
    public void DispenseByCashCounts(
        CashCount[] cashCounts,
        string currencyCode,
        decimal factor,
        bool asyncMode)
    {
        ArgumentNullException.ThrowIfNull(cashCounts);
        var dict = CashCountAdapter.ToDenominationDict(cashCounts, currencyCode, factor);

        var command = new DispenseCashCommand(
            dispenseController,
            inventory,
            hardwareStatusManager,
            depositController,
            dict,
            asyncMode);

        mediator.Execute(command);
    }

    /// <summary>保留中の出金操作をすべてキャンセルします。</summary>
    public void ClearOutput()
    {
        mediator.Execute(new ClearOutputCommand(dispenseController));
        mediator.IsBusy = false;
    }
}
