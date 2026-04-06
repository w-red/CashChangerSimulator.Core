using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device.PosForDotNet.Coordination;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.PosForDotNet.Commands;

/// <summary>在庫読み取り操作をカプセル化するコマンド。</summary>
public class ReadCashCountsCommand : IUposCommand
{
    private readonly Inventory inventory;
    private readonly string currencyCode;
    private readonly decimal currencyFactor;

    /// <summary><see cref="ReadCashCountsCommand"/> クラスの新しいインスタンスを初期化します。</summary>
    /// <param name="inventory">現金在庫。</param>
    /// <param name="currencyCode">通貨コード。</param>
    /// <param name="currencyFactor">通貨係数。</param>
    public ReadCashCountsCommand(Inventory inventory, string currencyCode, decimal currencyFactor)
    {
        this.inventory = inventory;
        this.currencyCode = currencyCode;
        this.currencyFactor = currencyFactor;
    }

    /// <summary>読み取った現金残高の結果を取得します。</summary>
    public CashCounts Result { get; private set; }

    /// <summary>コマンドを実行します。</summary>
    public void Execute() => ExecuteAsync().GetAwaiter().GetResult();

    /// <summary>コマンドを非同期で実行します。</summary>
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

    /// <summary>コマンドの実行条件を検証します。</summary>
    public void Verify(IUposMediator mediator)
    {
        mediator.VerifyState(mustBeClaimed: true, mustBeEnabled: true, mustNotBeBusy: true);
    }
}
