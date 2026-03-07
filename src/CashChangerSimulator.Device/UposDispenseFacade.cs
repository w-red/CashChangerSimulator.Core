using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Device.Commands;
using CashChangerSimulator.Device.Coordination;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device;

/// <summary>UPOS の出金操作を統合的に処理する Facade。</summary>
/// <remarks>
/// 金額指定の払い出し（DispenseChange）および金種指定の払い出し（DispenseCash）のリクエストを受け、
/// 適切なバリデーション（在庫確認、状態チェック）を行った後にコマンドを実行します。
/// </remarks>
public class UposDispenseFacade
{
    private readonly DispenseController _dispenseController;
    private readonly DepositController _depositController;
    private readonly HardwareStatusManager _hardwareStatusManager;
    private readonly Inventory _inventory;
    private readonly IUposMediator _mediator;
    private readonly ILogger _logger;

    /// <summary>必要なコントローラーとサービスを注入して Facade を初期化します。</summary>
    public UposDispenseFacade(
        DispenseController dispenseController,
        DepositController depositController,
        HardwareStatusManager hardwareStatusManager,
        Inventory inventory,
        IUposMediator mediator,
        ILogger logger)
    {
        _dispenseController = dispenseController;
        _depositController = depositController;
        _hardwareStatusManager = hardwareStatusManager;
        _inventory = inventory;
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>金額指定の出金を実行します。</summary>
    public void DispenseByAmount(
        int amount,
        string currencyCode,
        decimal factor,
        bool asyncMode,
        Action<ErrorCode, int, bool> onResult)
    {
        if (amount <= 0)
            throw new PosControlException("Amount must be positive", ErrorCode.Illegal);
 
        var decimalAmount = amount / factor;
        var command = new DispenseChangeCommand(
            _dispenseController,
            _hardwareStatusManager,
            _depositController,
            decimalAmount,
            asyncMode,
            (code, codeEx) => onResult(code, codeEx, asyncMode));
 
        _mediator.Execute(command);
    }
 
    public void DispenseByCashCounts(
        CashCount[] cashCounts,
        string currencyCode,
        decimal factor,
        bool asyncMode,
        Action<ErrorCode, int, bool> onResult)
    {
        var dict = CashCountAdapter.ToDenominationDict(cashCounts, currencyCode, factor);

        var command = new DispenseCashCommand(
            _dispenseController,
            _inventory,
            _hardwareStatusManager,
            _depositController,
            dict,
            asyncMode,
            (code, codeEx) => onResult(code, codeEx, asyncMode));

        _mediator.Execute(command);
    }

    /// <summary>保留中の出金操作をすべてキャンセルします。</summary>
    public void ClearOutput()
    {
        _mediator.Execute(new ClearOutputCommand(_dispenseController));
        _mediator.IsBusy = false;
    }

    private void ValidatePreConditions()
    {
    }
}
