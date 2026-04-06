using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Device.PosForDotNet.Coordination;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>在庫調整操作をカプセル化するコマンド。</summary>
public class AdjustCashCountsCommand : IUposCommand
{
    private readonly Inventory inventory;
    private readonly IEnumerable<CashCount> cashCounts;
    private readonly string currencyCode;
    private readonly decimal currencyFactor;
    private readonly HardwareStatusManager hardwareStatusManager;

    /// <summary><see cref="AdjustCashCountsCommand"/> クラスの新しいインスタンスを初期化します。</summary>
    /// <param name="inventory">現金在庫。</param>
    /// <param name="cashCounts">調整する金種と枚数のリスト。</param>
    /// <param name="currencyCode">通貨コード。</param>
    /// <param name="currencyFactor">通貨係数。</param>
    /// <param name="hardwareStatusManager">ハードウェア状態管理。</param>
    public AdjustCashCountsCommand(
        Inventory inventory,
        IEnumerable<CashCount> cashCounts,
        string currencyCode,
        decimal currencyFactor,
        HardwareStatusManager hardwareStatusManager)
    {
        this.inventory = inventory;
        this.cashCounts = cashCounts;
        this.currencyCode = currencyCode;
        this.currencyFactor = currencyFactor;
        this.hardwareStatusManager = hardwareStatusManager;
    }

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
