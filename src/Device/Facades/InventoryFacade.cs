using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device.Commands;
using CashChangerSimulator.Device.Coordination;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.Facades;

/// <summary>現金在庫の照会、調整（全回収）等の操作を統合的に処理する <see cref="InventoryFacade"/>。</summary>
/// <param name="inventory">在庫データを管理する <see cref="Inventory"/>。</param>
/// <param name="manager">デバイス全体の操作を管理する <see cref="CashChangerManager"/>。</param>
/// <param name="mediator">コマンド実行を仲介する <see cref="IUposMediator"/>。</param>
/// <remarks>
/// 各金種ごとの枚数取得、合計金額の計算、および在庫調整（全回収など）を行う機能を集約します。
/// </remarks>
public class InventoryFacade(Inventory inventory, CashChangerManager manager, IUposMediator mediator)
{
    /// <summary>現在の現金在庫数を読み取ります。</summary>
    /// <remarks>
    /// 指定された通貨コードの全金種の在庫を返します。
    /// </remarks>
    public CashCounts ReadCashCounts(string currencyCode, decimal currencyFactor)
    {
        var command = new ReadCashCountsCommand(inventory, currencyCode, currencyFactor);
        mediator.Execute(command);
        return command.Result;
    }
    
    /// <summary>現在の現金在庫数を手動で調整します。</summary>
    /// <remarks>指定された金種の枚数で現在の在庫を上書きします。</remarks>
    public void AdjustCashCounts(
        IEnumerable<CashCount> cashCounts,
        string currencyCode,
        decimal currencyFactor,
        HardwareStatusManager hardwareStatusManager)
    {
        var command = new AdjustCashCountsCommand(
            inventory,
            cashCounts,
            currencyCode,
            currencyFactor,
            hardwareStatusManager);
    
        mediator.Execute(command);
    }

    /// <summary>現在の現金在庫数を文字列形式で調整します。</summary>
    public void AdjustCashCounts(
        string cashCounts,
        string currencyCode,
        decimal currencyFactor,
        HardwareStatusManager hardwareStatusManager)
    {
        if (cashCounts == "discrepancy")
        {
            inventory.HasDiscrepancy = true;
            return;
        }

        var parsedCounts = CashCountAdapter.ParseCashCounts(cashCounts, currencyCode, currencyFactor, AllDenominationKeys);
        AdjustCashCounts(parsedCounts, currencyCode, currencyFactor, hardwareStatusManager);
    }
    
    /// <summary>リサイクル在庫をすべて回収庫へ移動します。</summary>
    public void PurgeCash()
    {
        mediator.Execute(new PurgeCashCommand(manager));
    }
    
    /// <summary>指定された通貨の現在庫リストを UPOS 形式で構築します。</summary>
    public CashUnits GetCashList(string currencyCode) =>
        UposCurrencyHelper.BuildCashUnits(inventory, currencyCode);

    // ========== Properties ==========

    /// <summary>現在の在庫に不一致があるかどうかを取得します。</summary>
    public bool HasDiscrepancy => inventory.HasDiscrepancy;

    /// <summary>アクティブな通貨のすべての現金単位のキーを取得します。</summary>
    public IEnumerable<DenominationKey> AllDenominationKeys => inventory.AllCounts.Select(kv => kv.Key);
}
