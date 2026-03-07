using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device.Commands;
using CashChangerSimulator.Device.Coordination;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.Facades;

/// <summary>UPOS の入金操作を統合的に処理する Facade。</summary>
/// <remarks>入金処理のすべての操作を集約し、SimulatorCashChanger から呼び出されます。</remarks>
public class DepositFacade
{
    private readonly DepositController _depositController;
    private readonly IUposMediator _mediator;
    private readonly DiagnosticController? _diagnosticController;

    /// <summary>新しいインスタンスを初期化します。</summary>
    public DepositFacade(DepositController depositController, IUposMediator mediator, DiagnosticController? diagnosticController = null)
    {
        _depositController = depositController ?? throw new ArgumentNullException(nameof(depositController));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _diagnosticController = diagnosticController;
    }

    /// <summary>入金処理を開始します。</summary>
    public void BeginDeposit(bool skipStateVerification)
    {
        _mediator.Execute(new BeginDepositCommand(_depositController), skipStateVerification);
    }
 
    /// <summary>入金処理を終了します。</summary>
    public void EndDeposit(CashDepositAction action, bool skipStateVerification)
    {
        _mediator.Execute(new EndDepositCommand(_depositController, action), skipStateVerification);
        
        // Increment successful depletion if not repaying
        if (action != CashDepositAction.NoChange) // Note: Renamed from Cleanup in previous step
        {
            _diagnosticController?.IncrementSuccessfulDepletion();
        }
    }
 
    /// <summary>投入された現金の計数を確定します。</summary>
    public void FixDeposit(bool skipStateVerification)
    {
        _mediator.Execute(new FixDepositCommand(_depositController), skipStateVerification);
    }
 
    /// <summary>入金処理を一時停止または再開します。</summary>
    public void PauseDeposit(CashDepositPause control, bool skipStateVerification)
    {
        _mediator.Execute(new PauseDepositCommand(_depositController, control), skipStateVerification);
    }
 
    /// <summary>入金セッション中に投入された現金を返却します。</summary>
    public void RepayDeposit(bool skipStateVerification)
    {
        _mediator.Execute(new RepayDepositCommand(_depositController), skipStateVerification);
    }

    // ========== Properties ==========

    /// <summary>現在投入されている現金の合計金額を取得します。</summary>
    public decimal DepositAmount => _depositController.DepositAmount;

    /// <summary>現在投入されている現金の金種別枚数を取得します。</summary>
    public IReadOnlyDictionary<DenominationKey, int> DepositCounts => _depositController.DepositCounts;

    /// <summary>現在の入金処理の状態を取得します。</summary>
    public CashDepositStatus DepositStatus => _depositController.DepositStatus;

    /// <summary>入金処理が進行中かどうかを取得します。</summary>
    public bool IsDepositInProgress => _depositController.IsDepositInProgress;

    /// <summary>リアルタイム入金通知が有効かどうかを取得または設定します。</summary>
    public bool RealTimeDataEnabled
    {
        get => _depositController.RealTimeDataEnabled;
        set => _depositController.RealTimeDataEnabled = value;
    }
}
