using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device.Commands;
using CashChangerSimulator.Device.Coordination;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.Facades;

/// <summary>UPOS の在庫管理操作を統合的に処理する Facade。</summary>
/// <remarks>在庫読み取り・調整・回収などのすべての操作を集約します。</remarks>
public class InventoryFacade
{
    private readonly Inventory _inventory;
    private readonly CashChangerManager _manager;
    private readonly IUposMediator _mediator;

    /// <summary>新しいインスタンスを初期化します。</summary>
    public InventoryFacade(Inventory inventory, CashChangerManager manager, IUposMediator mediator)
    {
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    /// <summary>現在の現金在庫数を読み取ります。</summary>
    /// <remarks>
    /// 指定された通貨コードの全金種の在庫を返します。
    /// </remarks>
    public CashCounts ReadCashCounts(string currencyCode, decimal currencyFactor, bool skipStateVerification)
    {
        var command = new ReadCashCountsCommand(_inventory, currencyCode, currencyFactor);
        _mediator.Execute(command, skipStateVerification);
        return command.Result;
    }
    
    /// <summary>現在の現金在庫数を手動で調整します。</summary>
    /// <remarks>指定された金種の枚数で現在の在庫を上書きします。</remarks>
    public void AdjustCashCounts(
        IEnumerable<CashCount> cashCounts,
        string currencyCode,
        decimal currencyFactor,
        HardwareStatusManager hardwareStatusManager,
        bool skipStateVerification)
    {
        var command = new AdjustCashCountsCommand(
            _inventory,
            cashCounts,
            currencyCode,
            currencyFactor,
            hardwareStatusManager);
    
        _mediator.Execute(command, skipStateVerification);
    }

    /// <summary>現在の現金在庫数を文字列形式で調整します。</summary>
    public void AdjustCashCounts(
        string cashCounts,
        string currencyCode,
        decimal currencyFactor,
        HardwareStatusManager hardwareStatusManager,
        bool skipStateVerification)
    {
        if (cashCounts == "discrepancy")
        {
            _inventory.HasDiscrepancy = true;
            return;
        }

        var parsedCounts = CashCountAdapter.ParseCashCounts(cashCounts, currencyCode, currencyFactor, AllDenominationKeys);
        AdjustCashCounts(parsedCounts, currencyCode, currencyFactor, hardwareStatusManager, skipStateVerification);
    }
    
    /// <summary>リサイクル在庫をすべて回収庫へ移動します。</summary>
    public void PurgeCash(bool skipStateVerification)
    {
        _mediator.Execute(new PurgeCashCommand(_manager), skipStateVerification);
    }

    // ========== Properties ==========

    /// <summary>現在の在庫に不一致があるかどうかを取得します。</summary>
    public bool HasDiscrepancy => _inventory.HasDiscrepancy;

    /// <summary>アクティブな通貨のすべての現金単位のキーを取得します。</summary>
    public IEnumerable<DenominationKey> AllDenominationKeys => _inventory.AllCounts.Select(kv => kv.Key);
}
