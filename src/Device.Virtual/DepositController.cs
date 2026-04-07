using System.Threading;
using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Services.DeviceEventTypes;
using Microsoft.Extensions.Logging;
using R3;
using ZLogger;

namespace CashChangerSimulator.Device.Virtual;

/// <summary>入金シーケンスのライフサイクルを管理するコントローラー（仮想デバイス実装）。</summary>
/// <remarks>
/// UPOS などのプラットフォーム固有の SDK に依存せず、純粋な C# ロジックとして入金プロセスをシミュレートします。
/// </remarks>
public class DepositController : IDisposable
{
    private readonly Inventory inventory;
    private readonly HardwareStatusManager hardwareStatusManager;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2213:DisposableFieldsShouldBeDisposed", Justification = "Shared or conditionally owned via internalConfigProvider.")]
    private readonly ConfigurationProvider configProvider;
    private readonly ConfigurationProvider? internalConfigProvider;
    private readonly CashChangerManager? manager;
    private readonly ILogger<DepositController> logger = LogProvider.CreateLogger<DepositController>();
    private readonly CompositeDisposable disposables = [];
    private readonly Subject<Unit> changed = new();
    private readonly Subject<DeviceDataEventArgs> dataEvents = new();
    private readonly Subject<DeviceErrorEventArgs> errorEvents = new();
    private readonly Lock stateLock = new();
    private readonly Dictionary<DenominationKey, int> depositCounts = [];
    private readonly List<string> depositedSerials = [];
    private readonly List<string> lastDepositedSerials = [];
    private CancellationTokenSource? depositCts;
    private decimal depositAmount;
    private decimal overflowAmount;
    private decimal rejectAmount;
    private DeviceDepositStatus depositStatus = DeviceDepositStatus.None;
    private bool depositPaused;
    private bool depositFixed;
    private bool isBusy;
    private DeviceErrorCode lastErrorCode = DeviceErrorCode.Success;
    private int lastErrorCodeExtended;
    private decimal requiredAmount;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DepositController"/> class.
    /// </summary>
    /// <param name="inventory">現金在庫。</param>
    /// <param name="hardwareStatusManager">ハードウェア状態管理。</param>
    /// <param name="manager">釣銭機マネージャー。</param>
    /// <param name="configProvider">設定プロバイダー。</param>
    public DepositController(
        Inventory inventory,
        HardwareStatusManager? hardwareStatusManager = null,
        CashChangerManager? manager = null,
        ConfigurationProvider? configProvider = null)
    {
        this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        this.hardwareStatusManager = hardwareStatusManager ?? new HardwareStatusManager();
        if (configProvider == null)
        {
            this.configProvider = new ConfigurationProvider();
            internalConfigProvider = this.configProvider;
        }
        else
        {
            this.configProvider = configProvider;
            internalConfigProvider = null;
        }

        this.manager = manager;
    }

    /// <summary>状態が変更されたときに通知されるストリーム。</summary>
    public virtual Observable<Unit> Changed => changed;

    /// <summary>データイベントの通知ストリーム。</summary>
    public virtual Observable<DeviceDataEventArgs> DataEvents => dataEvents;

    /// <summary>エラーイベントの通知ストリーム。</summary>
    public virtual Observable<DeviceErrorEventArgs> ErrorEvents => errorEvents;

    /// <summary>リアルタイムデータの通知。上位層（アダプター等）が利用します。</summary>
    public bool RealTimeDataEnabled { get; set; }

    /// <summary>投入された合計金額を取得します。</summary>
    public virtual decimal DepositAmount
    {
        get
        {
            lock (stateLock)
            {
                return depositAmount;
            }
        }
    }

    /// <summary>オーバーフロー（満杯等により収納不可）した金額を取得します。</summary>
    public virtual decimal OverflowAmount
    {
        get
        {
            lock (stateLock)
            {
                return overflowAmount;
            }
        }
    }

    /// <summary>リジェクト（偽札、汚れ等により返却）された金額を取得します。</summary>
    public virtual decimal RejectAmount
    {
        get
        {
            lock (stateLock)
            {
                return rejectAmount;
            }
        }
    }

    /// <summary>投入された各種金種の枚数を取得します。</summary>
    public virtual IReadOnlyDictionary<DenominationKey, int> DepositCounts
    {
        get
        {
            lock (stateLock)
            {
                return new Dictionary<DenominationKey, int>(depositCounts);
            }
        }
    }

    /// <summary>現在の預入状態を取得します。</summary>
    public virtual DeviceDepositStatus DepositStatus
    {
        get
        {
            lock (stateLock)
            {
                return depositStatus;
            }
        }
    }

    /// <summary>入金処理が進行中かどうかを取得します。</summary>
    public virtual bool IsDepositInProgress
    {
        get
        {
            lock (stateLock)
            {
                return depositStatus is DeviceDepositStatus.Start or DeviceDepositStatus.Counting or DeviceDepositStatus.Validation;
            }
        }
    }

    /// <summary>入金処理が一時停止中かどうかを取得します。</summary>
    public virtual bool IsPaused
    {
        get
        {
            lock (stateLock)
            {
                return depositPaused;
            }
        }
    }

    /// <summary>入金が確定（Fixed）されたかどうかを取得します。</summary>
    public virtual bool IsFixed
    {
        get
        {
            lock (stateLock)
            {
                return depositFixed;
            }
        }
    }

    /// <summary>デバイスがビジー状態かどうかを取得します。</summary>
    public virtual bool IsBusy
    {
        get
        {
            lock (stateLock)
            {
                return isBusy;
            }
        }
    }

    /// <summary>直近に発生したエラーコードを取得します。</summary>
    public virtual DeviceErrorCode LastErrorCode
    {
        get
        {
            lock (stateLock)
            {
                return lastErrorCode;
            }
        }
    }

    /// <summary>直近に発生した拡張エラーコードを取得します。</summary>
    public virtual int LastErrorCodeExtended
    {
        get
        {
            lock (stateLock)
            {
                return lastErrorCodeExtended;
            }
        }
    }

    /// <summary>直近に投入された紙幣のシリアル番号のリストを取得します。</summary>
    public virtual IReadOnlyList<string> LastDepositedSerials
    {
        get
        {
            lock (stateLock)
            {
                return [.. lastDepositedSerials];
            }
        }
    }

    /// <summary>必要入金金額を取得または設定します。</summary>
    public virtual decimal RequiredAmount
    {
        get
        {
            lock (stateLock)
            {
                return requiredAmount;
            }
        }

        set
        {
            lock (stateLock)
            {
                if (requiredAmount == value)
                {
                    return;
                }

                requiredAmount = value;
            }

            changed.OnNext(Unit.Default);
        }
    }

    /// <summary>預入（Deposit）処理を開始します。</summary>
    public virtual void BeginDeposit()
    {
        lock (stateLock)
        {
            logger.ZLogInformation($"BeginDeposit called. Current Status: {depositStatus}");

            if (isBusy)
            {
                throw new DeviceException("Device is busy", DeviceErrorCode.Busy);
            }

            if (hardwareStatusManager.IsJammed.Value)
            {
                throw new DeviceException("Device is jammed. Cannot begin deposit.", DeviceErrorCode.Jammed);
            }

            if (hardwareStatusManager.IsOverlapped.Value)
            {
                throw new DeviceException("Device has overlapped cash. Cannot begin deposit.", DeviceErrorCode.Overlapped);
            }

            depositAmount = 0m;
            overflowAmount = 0m;
            rejectAmount = 0m;
            depositCounts.Clear();
            depositedSerials.Clear();
            depositStatus = DeviceDepositStatus.Start;
            depositPaused = false;
            depositFixed = false;
            lastErrorCode = DeviceErrorCode.Success;
            lastErrorCodeExtended = 0;

            depositStatus = DeviceDepositStatus.Counting;
            inventory.ClearEscrow();
        }

        changed.OnNext(Unit.Default);
    }

    /// <summary>投入された金額を確定させます。</summary>
    public virtual void FixDeposit()
    {
        lock (stateLock)
        {
            if (depositStatus != DeviceDepositStatus.Counting)
            {
                throw new DeviceException("Counting is not in progress.", DeviceErrorCode.Illegal);
            }

            depositFixed = true;
            lastDepositedSerials.Clear();
            lastDepositedSerials.AddRange(depositedSerials);
        }

        changed.OnNext(Unit.Default);
    }

    /// <summary>入金処理を非同期で終了します。</summary>
    /// <param name="action">終了時のアクション（収納または返却）。</param>
    /// <returns>完了を示すタスク。</returns>
    public virtual async Task EndDepositAsync(DepositAction action)
    {
        lock (stateLock)
        {
            if (!depositFixed)
            {
                throw new DeviceException("Invalid call sequence: FixDeposit must be called before EndDeposit.", DeviceErrorCode.Illegal);
            }

            if (isBusy)
            {
                throw new DeviceException("Device is busy", DeviceErrorCode.Busy);
            }

            isBusy = true;
            lastErrorCode = DeviceErrorCode.Success;
            lastErrorCodeExtended = 0;
        }

        changed.OnNext(Unit.Default);
        await Task.Yield();

        if (depositCts != null)
        {
            await depositCts.CancelAsync().ConfigureAwait(false);
            depositCts.Dispose();
        }

        depositCts = new CancellationTokenSource();
        var token = depositCts.Token;

        try
        {
            await Task.Delay(500, token).ConfigureAwait(false);

            lock (stateLock)
            {
                if (action == DepositAction.Repay)
                {
                    logger.ZLogInformation($"Deposit Repay: Returning cash from escrow.");
                    inventory.ClearEscrow();
                }
                else if (action == DepositAction.Change)
                {
                    decimal changeAmount = Math.Max(0, depositAmount - RequiredAmount);
                    var storeCounts = new Dictionary<DenominationKey, int>(depositCounts);

                    if (changeAmount > 0)
                    {
                        var availableInEscrow = inventory.EscrowCounts.OrderByDescending(kv => kv.Key.Value).ToList();
                        decimal remainingChange = changeAmount;
                        foreach (var (key, countInEscrow) in availableInEscrow)
                        {
                            if (remainingChange <= 0)
                            {
                                break;
                            }

                            int useCount = (int)Math.Min(countInEscrow, Math.Floor(remainingChange / key.Value));
                            if (useCount > 0)
                            {
                                storeCounts[key] -= useCount;
                                remainingChange -= key.Value * useCount;
                            }
                        }

                        inventory.ClearEscrow();
                        foreach (var kv in storeCounts)
                        {
                            if (kv.Value > 0)
                            {
                                inventory.AddEscrow(kv.Key, kv.Value);
                            }
                        }

                        if (remainingChange > 0 && manager != null)
                        {
                            manager.Dispense(remainingChange);
                        }
                    }
                    else
                    {
                        inventory.ClearEscrow();
                    }

                    if (manager != null)
                    {
                        manager.Deposit(new Dictionary<DenominationKey, int>(storeCounts));
                    }
                    else
                    {
                        foreach (var kv in storeCounts)
                        {
                            if (kv.Value > 0)
                            {
                                inventory.Add(kv.Key, kv.Value);
                            }
                        }
                    }
                }
                else // NoChange (or None)
                {
                    logger.ZLogInformation($"Deposit NoChange: Storing all cash into inventory.");
                    var storeCounts = new Dictionary<DenominationKey, int>(depositCounts);
                    inventory.ClearEscrow();

                    if (manager != null)
                    {
                        manager.Deposit(new Dictionary<DenominationKey, int>(storeCounts));
                    }
                    else
                    {
                        foreach (var kv in storeCounts)
                        {
                            if (kv.Value > 0)
                            {
                                inventory.Add(kv.Key, kv.Value);
                            }
                        }
                    }
                }

                if (action != DepositAction.Repay && hardwareStatusManager.IsOverlapped.Value)
                {
                    throw new DeviceException("Device Error (Overlap). Cannot complete deposit.", DeviceErrorCode.Overlapped);
                }

                depositStatus = DeviceDepositStatus.End;
                depositPaused = false;
                depositFixed = false;

                if (action == DepositAction.Repay)
                {
                    depositAmount = 0m;
                    depositCounts.Clear();
                }

                inventory.ClearEscrow();
            }
        }
        catch (OperationCanceledException)
        {
            lock (stateLock)
            {
                lastErrorCode = DeviceErrorCode.Cancelled;
            }
        }
        catch (DeviceException dex)
        {
            logger.ZLogError(dex, $"EndDeposit failed with device error.");
            lock (stateLock)
            {
                lastErrorCode = dex.ErrorCode;
                lastErrorCodeExtended = dex.ErrorCodeExtended;
                errorEvents.OnNext(new DeviceErrorEventArgs(lastErrorCode, lastErrorCodeExtended, DeviceErrorLocus.Output, DeviceErrorResponse.Retry));
            }
        }
        catch (Exception ex)
        {
            logger.ZLogError(ex, $"EndDeposit failed with unexpected error.");
            lock (stateLock)
            {
                lastErrorCode = DeviceErrorCode.Failure;
                lastErrorCodeExtended = 0;
                errorEvents.OnNext(new DeviceErrorEventArgs(lastErrorCode, 0, DeviceErrorLocus.Output, DeviceErrorResponse.Retry));
            }
        }
        finally
        {
            lock (stateLock)
            {
                isBusy = false;
                if (!disposed)
                {
                    changed.OnNext(Unit.Default);
                }
            }
        }
    }

    /// <summary>入金を終了します（同期ラッパー）。</summary>
    /// <param name="action">終了時のアクション（収納または返却）。</param>
    public virtual void EndDeposit(DepositAction action)
    {
        EndDepositAsync(action).GetAwaiter().GetResult();
        if (LastErrorCode != DeviceErrorCode.Success)
        {
            throw new DeviceException("EndDeposit failed.", LastErrorCode, LastErrorCodeExtended);
        }
    }

    /// <summary>投入された現金を返却し、入金セッションを終了します。</summary>
    /// <returns>完了を示すタスク。</returns>
    public virtual async Task RepayDepositAsync()
    {
        bool needsFix = false;
        lock (stateLock)
        {
            needsFix = !depositFixed;
        }

        if (needsFix)
        {
            FixDeposit();
        }

        await EndDepositAsync(DepositAction.Repay)
            .ConfigureAwait(false);
    }

    /// <summary>投入された現金を返却し、入金セッションを終了します（同期ラッパー）。</summary>
    public virtual void RepayDeposit()
    {
        RepayDepositAsync().GetAwaiter().GetResult();
        if (LastErrorCode != DeviceErrorCode.Success)
        {
            throw new DeviceException("RepayDeposit failed.", LastErrorCode, LastErrorCodeExtended);
        }
    }

    /// <summary>入金処理を一時停止または再開します。</summary>
    /// <param name="control">一時停止または再開。</param>
    public virtual void PauseDeposit(DeviceDepositPause control)
    {
        lock (stateLock)
        {
            if (!IsDepositInProgress)
            {
                throw new DeviceException("Session not active.", DeviceErrorCode.Illegal);
            }

            bool requestedPause = control == DeviceDepositPause.Pause;
            if (depositPaused == requestedPause)
            {
                throw new DeviceException($"Device is already {(requestedPause ? "paused" : "running")}.", DeviceErrorCode.Illegal);
            }

            depositPaused = requestedPause;
        }

        changed.OnNext(Unit.Default);
    }

    /// <summary>単一の金種の投入を追跡します。</summary>
    /// <param name="key">金種。</param>
    /// <param name="count">枚数。</param>
    public void TrackDeposit(DenominationKey key, int count = 1)
    {
        ArgumentNullException.ThrowIfNull(key);
        TrackBulkDeposit(new Dictionary<DenominationKey, int> { { key, count } });
    }

    /// <summary>複数の金種の投入を一括で追跡します。</summary>
    /// <param name="counts">金種と枚数のセット。</param>
    public void TrackBulkDeposit(IReadOnlyDictionary<DenominationKey, int> counts)
    {
        ArgumentNullException.ThrowIfNull(counts);
        lock (stateLock)
        {
            if (depositStatus != DeviceDepositStatus.Counting
                || depositPaused)
            {
                return;
            }

            if (depositFixed)
            {
                throw new DeviceException("Deposit is already fixed.", DeviceErrorCode.Illegal);
            }

            if (hardwareStatusManager.IsJammed.Value)
            {
                throw new DeviceException("Device is jammed during tracking.", DeviceErrorCode.Jammed);
            }

            if (hardwareStatusManager.IsOverlapped.Value)
            {
                throw new DeviceException("Device has overlapped cash. Cannot track deposit.", DeviceErrorCode.Overlapped);
            }

            var config = configProvider.Config;

            foreach (var kv in counts)
            {
                if (kv.Value <= 0)
                {
                    continue;
                }

                if (!depositCounts.TryGetValue(kv.Key, out int currentDepositCount))
                {
                    currentDepositCount = 0;
                }

                int capacity = config.Thresholds.Full;
                if (config.Inventory.TryGetValue(kv.Key.CurrencyCode, out var invSettings) &&
                    invSettings.Denominations.TryGetValue(kv.Key.ToDenominationString(), out var denSettings))
                {
                    capacity = denSettings.Full;
                }

                int currentInventoryCount = inventory.GetCount(kv.Key);
                int totalBeforeDeposit = currentInventoryCount + currentDepositCount;
                int totalAfterDeposit = totalBeforeDeposit + kv.Value;

                int previouslyOverflowed = Math.Max(0, totalBeforeDeposit - capacity);
                int overflowCount = Math.Max(0, totalAfterDeposit - capacity);
                int newlyOverflowed = overflowCount - previouslyOverflowed;

                depositCounts[kv.Key] = currentDepositCount + kv.Value;
                depositAmount += kv.Key.Value * kv.Value;

                if (newlyOverflowed > 0)
                {
                    overflowAmount += kv.Key.Value * newlyOverflowed;
                }

                for (int i = 0; i < kv.Value; i++)
                {
                    depositedSerials.Add($"SN-{kv.Key.Value}-{Guid.NewGuid().ToString()[..8]}");
                }

                inventory.AddEscrow(kv.Key, kv.Value);
            }

            if (RealTimeDataEnabled)
            {
                dataEvents.OnNext(new DeviceDataEventArgs(0));
            }
        }

        changed.OnNext(Unit.Default);
    }

    /// <summary>指定された金額に近い金種をリジェクト庫（返却用）に投入します。</summary>
    /// <param name="amount">リジェクトする合計金額。</param>
    public void TrackReject(decimal amount)
    {
        lock (stateLock)
        {
            if (!IsDepositInProgress || depositPaused)
            {
                return;
            }

            rejectAmount += amount;
        }

        if (RealTimeDataEnabled)
        {
            dataEvents.OnNext(new DeviceDataEventArgs(0));
        }

        changed.OnNext(Unit.Default);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>リソースを破棄します。</summary>
    /// <param name="disposing">マネージリソースを破棄する場合は true。</param>
    protected virtual void Dispose(bool disposing)
    {
        lock (stateLock)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
        }

        if (disposing)
        {
            depositCts?.Cancel();
            depositCts?.Dispose();
            disposables.Dispose();
            changed.OnCompleted();
            changed.Dispose();
            dataEvents.OnCompleted();
            dataEvents.Dispose();
            errorEvents.OnCompleted();
            errorEvents.Dispose();
            internalConfigProvider?.Dispose();

            // Note: Injected dependencies (inventory, hardwareStatusManager) should not be disposed here.
        }
    }
}
