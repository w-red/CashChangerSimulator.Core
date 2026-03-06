using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Device.Coordination;
using Microsoft.PointOfService;
using System.Collections.Generic;

namespace CashChangerSimulator.Device.Commands;

/// <summary>在庫調整操作をカプセル化するコマンド。</summary>
public class AdjustCashCountsCommand : IUposCommand
{
    private readonly Inventory _inventory;
    private readonly IEnumerable<CashCount> _cashCounts;
    private readonly string _currencyCode;
    private readonly decimal _currencyFactor;
    private readonly HardwareStatusManager _hardwareStatusManager;

    public AdjustCashCountsCommand(
        Inventory inventory,
        IEnumerable<CashCount> cashCounts,
        string currencyCode,
        decimal currencyFactor,
        HardwareStatusManager hardwareStatusManager)
    {
        _inventory = inventory;
        _cashCounts = cashCounts;
        _currencyCode = currencyCode;
        _currencyFactor = currencyFactor;
        _hardwareStatusManager = hardwareStatusManager;
    }

    public void Execute()
    {
        if (_hardwareStatusManager.IsJammed.Value)
        {
            throw new PosControlException(
                "Device is jammed. Cannot adjust cash counts.",
                ErrorCode.Extended,
                (int)UposCashChangerErrorCodeExtended.Jam);
        }

        var dict = CashCountAdapter.ToDenominationDict(_cashCounts, _currencyCode, _currencyFactor);
        foreach (var (key, count) in dict)
        {
            _inventory.SetCount(key, count);
        }
    }

    public void Verify(IUposMediator mediator, bool skipStateVerification)
    {
        mediator.VerifyState(skipStateVerification, mustBeClaimed: true, mustBeEnabled: true, mustNotBeBusy: true);
    }
}
