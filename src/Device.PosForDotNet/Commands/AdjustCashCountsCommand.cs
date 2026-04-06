using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Device.PosForDotNet.Coordination;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>在庫調整操作をカプセル化するコマンド。</summary>
public class AdjustCashCountsCommand(
    Inventory inventory,
    IEnumerable<CashCount> cashCounts,
    string currencyCode,
    decimal currencyFactor,
    HardwareStatusManager hardwareStatusManager) : IUposCommand
{

    /// <inheritdoc/>
    public void Execute() =>
        ExecuteAsync().GetAwaiter().GetResult();

    /// <inheritdoc/>
    public Task ExecuteAsync()
    {
        if (hardwareStatusManager.IsJammed.Value)
        {
            throw new PosControlException(
                "Device is jammed. Cannot adjust cash counts.",
                ErrorCode.Extended,
                (int)UposCashChangerErrorCodeExtended.Jam);
        }

        var dict = CashCountAdapter.ToDenominationDict(cashCounts, currencyCode, currencyFactor);
        foreach (var (key, count) in dict)
        {
            inventory.SetCount(key, count);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Verify(IUposMediator mediator) =>
        mediator
        .VerifyState(
            mustBeClaimed: true,
            mustBeEnabled: true,
            mustNotBeBusy: true);
}
