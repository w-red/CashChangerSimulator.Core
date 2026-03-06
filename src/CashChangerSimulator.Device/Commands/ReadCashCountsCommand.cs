using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Device.Coordination;
using Microsoft.PointOfService;
using System.Linq;

namespace CashChangerSimulator.Device.Commands;

/// <summary>在庫読み取り操作をカプセル化するコマンド。</summary>
public class ReadCashCountsCommand : IUposCommand
{
    private readonly Inventory _inventory;
    private readonly string _currencyCode;
    private readonly decimal _currencyFactor;

    public ReadCashCountsCommand(Inventory inventory, string currencyCode, decimal currencyFactor)
    {
        _inventory = inventory;
        _currencyCode = currencyCode;
        _currencyFactor = currencyFactor;
    }

    public CashCounts Result { get; private set; }

    public void Execute()
    {
        var sorted = _inventory.AllCounts
            .Where(kv => kv.Key.CurrencyCode == _currencyCode)
            .OrderBy(kv => kv.Key.Type)
            .ThenBy(kv => kv.Key.Value);

        var list = sorted
            .Select(kv => CashCountAdapter.ToCashCount(kv.Key, kv.Value, _currencyFactor))
            .ToList();

        Result = new CashCounts([.. list], _inventory.HasDiscrepancy);
    }

    public void Verify(IUposMediator mediator, bool skipStateVerification)
    {
        mediator.VerifyState(skipStateVerification, mustBeClaimed: true, mustBeEnabled: true, mustNotBeBusy: true);
    }
}
