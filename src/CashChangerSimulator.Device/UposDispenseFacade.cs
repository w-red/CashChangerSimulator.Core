using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Opos;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using MoneyKind4Opos.Currencies.Interfaces;
using ZLogger;

namespace CashChangerSimulator.Device;

/// <summary>UPOS の出金操作を統合的に処理する Facade。</summary>
public class UposDispenseFacade(
    DispenseController dispenseController,
    DepositController depositController,
    HardwareStatusManager hardwareStatusManager,
    Inventory inventory,
    ILogger logger)
{
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

        var task = dispenseController.DispenseChangeAsync(
            decimalAmount, asyncMode, (code, codeEx) => onResult(code, codeEx, asyncMode), currencyCode);

        if (!asyncMode)
            task.GetAwaiter().GetResult();
    }

    /// <summary>金種指定の出金を実行します。</summary>
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
            if (!inventory.AllCounts.Any(kv => kv.Key == key))
                throw new PosControlException(
                    $"Denomination {key} is not registered for the current currency ({currencyCode}).",
                    ErrorCode.Illegal);

            if (inventory.GetCount(key) < count)
                throw new PosControlException(
                    $"Insufficient inventory for {key}. Required: {count}, Available: {inventory.GetCount(key)}",
                    ErrorCode.Extended,
                    (int)UposCashChangerErrorCodeExtended.OverDispense);
        }

        var task = dispenseController.DispenseCashAsync(
            dict, asyncMode, (code, codeEx) => onResult(code, codeEx, asyncMode));

        if (!asyncMode)
            task.GetAwaiter().GetResult();
    }

    private void ValidatePreConditions()
    {
        if (depositController.IsDepositInProgress)
            throw new PosControlException(
                "Cash cannot be dispensed because cash acceptance is in progress.",
                ErrorCode.Illegal);

        if (hardwareStatusManager.IsJammed.Value)
            throw new PosControlException(
                "Device is jammed. Cannot dispense.",
                ErrorCode.Extended,
                (int)UposCashChangerErrorCodeExtended.Jam);
    }
}
