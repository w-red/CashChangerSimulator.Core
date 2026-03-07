using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Device.Commands;
using CashChangerSimulator.Device.Coordination;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device;

/// <summary>UPOS の出金操作を統合的に処理する Facade。</summary>
public class UposDispenseFacade
{
    private readonly DispenseController _dispenseController;
    private readonly DepositController _depositController;
    private readonly HardwareStatusManager _hardwareStatusManager;
    private readonly Inventory _inventory;
    private readonly IUposMediator _mediator;
    private readonly ILogger _logger;

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
 
        ValidatePreConditions();
 
        var decimalAmount = amount / factor;
        var command = new DispenseChangeCommand(
            _dispenseController,
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
        ValidatePreConditions();

        var dict = CashCountAdapter.ToDenominationDict(cashCounts, currencyCode, factor);

        // Validate inventory
        foreach (var (key, count) in dict)
        {
            if (!_inventory.AllCounts.Any(kv => kv.Key == key))
                throw new PosControlException(
                    $"Denomination {key} is not registered for the current currency ({currencyCode}).",
                    ErrorCode.Illegal);

            if (_inventory.GetCount(key) < count)
                throw new PosControlException(
                    $"Insufficient inventory for {key}. Required: {count}, Available: {_inventory.GetCount(key)}",
                    ErrorCode.Extended,
                    (int)UposCashChangerErrorCodeExtended.OverDispense);
        }

        var command = new DispenseCashCommand(
            _dispenseController,
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
        if (_depositController.IsDepositInProgress)
            throw new PosControlException(
                "Cash cannot be dispensed because cash acceptance is in progress.",
                ErrorCode.Illegal);

        if (_hardwareStatusManager.IsJammed.Value)
            throw new PosControlException(
                "Device is jammed. Cannot dispense.",
                ErrorCode.Extended,
                (int)UposCashChangerErrorCodeExtended.Jam);
    }
}
