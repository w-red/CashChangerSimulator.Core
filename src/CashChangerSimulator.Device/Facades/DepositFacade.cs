using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device.Commands;
using CashChangerSimulator.Device.Coordination;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.Facades;

/// <summary>UPOS の入金操作を統合的に処理する <see cref="DepositFacade"/>。</summary>
/// <param name="depositController">入金処理を制御する <see cref="DepositController"/>。</param>
/// <param name="mediator">コマンド実行を仲介する <see cref="IUposMediator"/>。</param>
/// <param name="diagnosticController">診断情報（統計など）を管理するコントローラー。</param>
/// <remarks>
/// 入金処理のすべての操作、および <see cref="SimulatorCashChanger"/> から呼び出されるコマンドの実行を集約します。
/// </remarks>
public class DepositFacade(
    DepositController depositController,
    IUposMediator mediator,
    DiagnosticController? diagnosticController = null)
{

    /// <summary>入金処理を開始します。</summary>
    public void BeginDeposit()
    {
        mediator.Execute(new BeginDepositCommand(depositController));
    }
 
    /// <summary>入金処理を終了します。</summary>
    public void EndDeposit(CashDepositAction action)
    {
        mediator.Execute(new EndDepositCommand(depositController, action));
        
        // Increment successful depletion if not repaying
        if (action != CashDepositAction.NoChange) // Note: Renamed from Cleanup in previous step
        {
            diagnosticController?.IncrementSuccessfulDepletion();
        }
    }
 
    /// <summary>投入された現金の計数を確定します。</summary>
    public void FixDeposit()
    {
        mediator.Execute(new FixDepositCommand(depositController));
    }
 
    /// <summary>入金処理を一時停止または再開します。</summary>
    public void PauseDeposit(CashDepositPause control)
    {
        mediator.Execute(new PauseDepositCommand(depositController, control));
    }
 
    /// <summary>入金セッション中に投入された現金を返却します。</summary>
    public void RepayDeposit()
    {
        mediator.Execute(new RepayDepositCommand(depositController));
    }

    // ========== Properties ==========

    /// <summary>現在投入されている現金の合計金額を取得します。</summary>
    public decimal DepositAmount => depositController.DepositAmount;

    /// <summary>現在投入されている現金の合計金額をUPOS形式（整数）で取得します。</summary>
    public int GetUposDepositAmount(decimal factor)
    {
        return (int)Math.Round(DepositAmount * factor);
    }

    /// <summary>現在投入されている現金の金種別枚数を取得します。</summary>
    public IReadOnlyDictionary<DenominationKey, int> DepositCounts => depositController.DepositCounts;

    /// <summary>指定された通貨の入金情報を UPOS 形式で取得します。</summary>
    public CashCount[] GetUposDepositCounts(string currencyCode, decimal factor)
    {
        return [.. DepositCounts
            .Where(kv => kv.Key.CurrencyCode == currencyCode)
            .Select(kv => CashCountAdapter.ToCashCount(kv.Key, kv.Value, factor))];
    }

    /// <summary>現在の入金処理の状態を取得します。</summary>
    public CashDepositStatus DepositStatus => depositController.DepositStatus;

    /// <summary>入金処理が進行中かどうかを取得します。</summary>
    public bool IsDepositInProgress => depositController.IsDepositInProgress;

    /// <summary>リアルタイム入金通知が有効かどうかを取得または設定します。</summary>
    public bool RealTimeDataEnabled
    {
        get => depositController.RealTimeDataEnabled;
        set => depositController.RealTimeDataEnabled = value;
    }
}
