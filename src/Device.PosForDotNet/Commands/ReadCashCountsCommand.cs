using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device.PosForDotNet.Coordination;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>在庫読み取り操作をカプセル化するコマンド。.</summary>
public class ReadCashCountsCommand : IUposCommand
{
    private readonly Inventory inventory;
    private readonly string currencyCode;
    private readonly decimal currencyFactor;

    /// <inheritdoc/>
    public ReadCashCountsCommand(Inventory inventory, string currencyCode, decimal currencyFactor)
    {
        this.inventory = inventory;
        this.currencyCode = currencyCode;
        this.currencyFactor = currencyFactor;
    }

    /// <inheritdoc/>
    public CashCounts Result { get; private set; }

    /// <inheritdoc/>
    public void Execute() => ExecuteAsync().GetAwaiter().GetResult();

    /// <inheritdoc/>
    public Task ExecuteAsync()
    {
        var sorted = inventory.AllCounts
            .Where(kv => kv.Key.CurrencyCode == currencyCode)
            .OrderBy(kv => kv.Key.Type)
            .ThenBy(kv => kv.Key.Value);

        var list = sorted
            .Select(kv => CashCountAdapter.ToCashCount(kv.Key, kv.Value, currencyFactor))
            .ToList();

        Result = new CashCounts([.. list], inventory.HasDiscrepancy);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Verify(IUposMediator mediator)
    {
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: true, mustNotBeBusy: true);
    }
}
