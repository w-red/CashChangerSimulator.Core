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
public class LifecycleManager(
    HardwareStatusManager hardwareStatusManager,
    IUposMediator mediator,
    TransactionHistory history,
    ILogger logger) : IUposLifecycleHandler
{
    private IUposLifecycleHandler handler = null!;

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
