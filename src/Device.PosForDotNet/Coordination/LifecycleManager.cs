using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Transactions;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using ZLogger;

namespace CashChangerSimulator.Device.PosForDotNet.Coordination;

/// <summary>UPOS ライフサイクルの管理とハンドラの切り替えを担当するマネージャークラス。</summary>
/// <remarks>
/// デバイスの Open, Claim, Enable 等の状態遷移を管理します。
/// 状態検証のスキップ設定に応じて、標準のバリデーションを行うハンドラと、検証をバイパスするハンドラを動的に切り替えます。
/// </remarks>
public class LifecycleManager : IUposLifecycleHandler
{
    private readonly HardwareStatusManager hardwareStatusManager;
    private readonly IUposMediator mediator;
    private readonly TransactionHistory history;
    private readonly ILogger logger;
    private IUposLifecycleHandler handler = null!;

    /// <summary>Initializes a new instance of the <see cref="LifecycleManager"/> class.必要な依存関係を注入してマネージャーを初期化します。</summary>
    /// <remarks>状態管理、バリデーション、およびロギングのためのコンポーネントを受け取ります。</remarks>
    public LifecycleManager(HardwareStatusManager hardwareStatusManager, IUposMediator mediator, TransactionHistory history, ILogger logger)
    {
        this.hardwareStatusManager = hardwareStatusManager ?? throw new ArgumentNullException(nameof(hardwareStatusManager));
        this.mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        this.history = history ?? throw new ArgumentNullException(nameof(history));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>検証スキップ設定に基づいて適切なハンドラを初期化します。</summary>
    public void UpdateHandler(bool skipStateVerification)
    {
        handler = skipStateVerification
            ? new SkipVerificationLifecycleHandler(hardwareStatusManager, mediator, history, logger)
            : new StandardLifecycleHandler(hardwareStatusManager, mediator, history, logger);

        logger.ZLogInformation($"LifecycleHandler switched. SkipStateVerification: {skipStateVerification}, HandlerType: {handler.GetType().Name}");
    }

    /// <inheritdoc/>
    public ControlState State => handler?.State ?? ControlState.Closed;

    /// <inheritdoc/>
    public bool Claimed => handler?.Claimed ?? false;

    /// <inheritdoc/>
    public bool DeviceEnabled { get => handler.DeviceEnabled; set => handler.DeviceEnabled = value; }

    /// <inheritdoc/>
    public bool DataEventEnabled { get => handler.DataEventEnabled; set => handler.DataEventEnabled = value; }

    /// <inheritdoc/>
    public void Open(Action baseOpen) => handler.Open(baseOpen);

    /// <inheritdoc/>
    public void Close(Action baseClose) => handler.Close(baseClose);

    /// <inheritdoc/>
    public void Claim(int timeout, Action<int> baseClaim) => handler.Claim(timeout, baseClaim);

    /// <inheritdoc/>
    public void Release(Action baseRelease) => handler.Release(baseRelease);
}
