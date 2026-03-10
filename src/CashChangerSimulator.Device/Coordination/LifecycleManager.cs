using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Device.Models;
using ZLogger;
using CashChangerSimulator.Core.Transactions;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;

namespace CashChangerSimulator.Device.Coordination;

/// <summary>UPOS ライフサイクルの管理とハンドラの切り替えを担当するマネージャークラス。</summary>
/// <remarks>
/// デバイスの Open, Claim, Enable 等の状態遷移を管理します。
/// 状態検証のスキップ設定に応じて、標準のバリデーションを行うハンドラと、検証をバイパスするハンドラを動的に切り替えます。
/// </remarks>
public class LifecycleManager : IUposLifecycleHandler
{
    private readonly HardwareStatusManager _hardwareStatusManager;
    private readonly IUposMediator _mediator;
    private readonly TransactionHistory _history;
    private readonly ILogger _logger;
    private IUposLifecycleHandler _handler = null!;

    /// <summary>必要な依存関係を注入してマネージャーを初期化します。</summary>
    /// <remarks>状態管理、バリデーション、およびロギングのためのコンポーネントを受け取ります。</remarks>
    public LifecycleManager(HardwareStatusManager hardwareStatusManager, IUposMediator mediator, TransactionHistory history, ILogger logger)
    {
        _hardwareStatusManager = hardwareStatusManager ?? throw new ArgumentNullException(nameof(hardwareStatusManager));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>検証スキップ設定に基づいて適切なハンドラを初期化します。</summary>
    public void UpdateHandler(bool skipStateVerification)
    {
        _handler = skipStateVerification
            ? new SkipVerificationLifecycleHandler(_hardwareStatusManager, _mediator, _history, _logger)
            : new StandardLifecycleHandler(_hardwareStatusManager, _mediator, _history, _logger);
        
        _logger.ZLogInformation($"LifecycleHandler switched. SkipStateVerification: {skipStateVerification}, HandlerType: {_handler.GetType().Name}");
    }

    public ControlState State => _handler?.State ?? ControlState.Closed;
    public bool Claimed => _handler?.Claimed ?? false;
    public bool DeviceEnabled { get => _handler.DeviceEnabled; set => _handler.DeviceEnabled = value; }
    public bool DataEventEnabled { get => _handler.DataEventEnabled; set => _handler.DataEventEnabled = value; }

    public void Open(Action baseOpen) => _handler.Open(baseOpen);
    public void Close(Action baseClose) => _handler.Close(baseClose);
    public void Claim(int timeout, Action<int> baseClaim) => _handler.Claim(timeout, baseClaim);
    public void Release(Action baseRelease) => _handler.Release(baseRelease);
}
