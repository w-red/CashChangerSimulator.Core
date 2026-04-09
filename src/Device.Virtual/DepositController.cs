using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
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
    private readonly TimeProvider timeProvider;
    private readonly ILogger<DepositController>? logger = LogProvider.CreateLogger<DepositController>();
    private readonly CompositeDisposable disposables = [];
    private readonly Lock stateLock = new();
    private readonly Dictionary<DenominationKey, int> depositCounts = [];
    private readonly List<string> depositedSerials = [];
    private readonly List<string> lastDepositedSerials = [];
    private CancellationTokenSource? depositCts;
    private bool disposed;

    /// <summary>依存コンポーネントを指定してインスタンスを初期化します。</summary>
    /// <param name="inventory">現金在庫。</param>
    /// <param name="hardwareStatusManager">ハードウェア状態管理。</param>
    /// <param name="manager">釣銭機マネージャー。</param>
    /// <param name="configProvider">設定プロバイダー。</param>
    /// <param name="timeProvider">時間プロバイダー。</param>
    public DepositController(
        Inventory inventory,
        HardwareStatusManager? hardwareStatusManager = null,
        CashChangerManager? manager = null,
        ConfigurationProvider? configProvider = null,
        TimeProvider? timeProvider = null)
    {
        this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        this.hardwareStatusManager = hardwareStatusManager ?? HardwareStatusManager.Create();
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
        this.timeProvider = timeProvider ?? TimeProvider.System;

        var changedSubject = new Subject<Unit>();
        disposables.Add(changedSubject);
        Changed = changedSubject;
        var dataEventsSubject = new Subject<DeviceDataEventArgs>();
        disposables.Add(dataEventsSubject);
        DataEvents = dataEventsSubject;
        var errorEventsSubject = new Subject<DeviceErrorEventArgs>();
        disposables.Add(errorEventsSubject);
        ErrorEvents = errorEventsSubject;

        // Initialize properties
        DepositStatus = DeviceDepositStatus.None;
        LastErrorCode = DeviceErrorCode.Success;
    }

    /// <summary>状態が変更されたときに通知されるストリーム。</summary>
    public virtual Observable<Unit> Changed { get; }

    /// <summary>データイベントの通知ストリーム。</summary>
    public virtual Observable<DeviceDataEventArgs> DataEvents { get; }

    /// <summary>エラーイベントの通知ストリーム。</summary>
    public virtual Observable<DeviceErrorEventArgs> ErrorEvents { get; }

    /// <summary>リアルタイムデータの通知。上位層（アダプター等）が利用します。</summary>
    public bool RealTimeDataEnabled { get; set; }

    /// <summary>投入された合計金額を取得します。</summary>
    public virtual decimal DepositAmount
    {
        get
        {
            lock (stateLock)
            {
                return field;
            }
        }

        set
        {
            lock (stateLock)
            {
                field = value;
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
                return field;
            }
        }

        set
        {
            lock (stateLock)
            {
                field = value;
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
                return field;
            }
        }

        set
        {
            lock (stateLock)
            {
                field = value;
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
                return field;
            }
        }

        private set
        {
            lock (stateLock)
            {
                field = value;
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
                return DepositStatus is
                    DeviceDepositStatus.Start or
                    DeviceDepositStatus.Counting or
                    DeviceDepositStatus.Validation;
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
                return field;
            }
        }

        private set
        {
            lock (stateLock)
            {
                field = value;
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
                return field;
            }
        }

        private set
        {
            lock (stateLock)
            {
                field = value;
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
                return field;
            }
        }

        private set
        {
            lock (stateLock)
            {
                field = value;
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
                return field;
            }
        }

        private set
        {
            lock (stateLock)
            {
                field = value;
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
                return field;
            }
        }

        private set
        {
            lock (stateLock)
            {
                field = value;
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
                return field;
            }
        }

        set
        {
            lock (stateLock)
            {
                if (field == value)
                {
                    return;
                }

                field = value;

                if (!disposed)
                {
                    ((Subject<Unit>)Changed).OnNext(Unit.Default);
                }
            }
        }
    }

    /// <summary>預入（Deposit）処理を開始します。</summary>
    public virtual void BeginDeposit()
    {
        lock (stateLock)
        {
            if (logger != null)
            {
                logger.ZLogInformation($"BeginDeposit called. Current Status: {DepositStatus}");
            }

            if (IsBusy)
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

            DepositAmount = 0m;
            OverflowAmount = 0m;
            RejectAmount = 0m;
            depositCounts.Clear();
            depositedSerials.Clear();
            DepositStatus = DeviceDepositStatus.Start;
            IsPaused = false;
            IsFixed = false;
            LastErrorCode = DeviceErrorCode.Success;
            LastErrorCodeExtended = 0;

            DepositStatus = DeviceDepositStatus.Counting;
            inventory.ClearEscrow();
            if (!disposed)
            {
                ((Subject<Unit>)Changed).OnNext(Unit.Default);
            }
        }
    }

    /// <summary>投入された金額を確定させます。</summary>
    public virtual void FixDeposit()
    {
        lock (stateLock)
        {
            if (DepositStatus != DeviceDepositStatus.Counting)
            {
                throw new DeviceException("Counting is not in progress.", DeviceErrorCode.Illegal);
            }

            IsFixed = true;
            lastDepositedSerials.Clear();
            lastDepositedSerials.AddRange(depositedSerials);
            if (!disposed)
            {
                ((Subject<Unit>)Changed).OnNext(Unit.Default);
            }
        }
    }

    /// <summary>入金処理を非同期で終了します。</summary>
    /// <param name="action">終了時のアクション（収納または返却）。</param>
    /// <returns>完了を示すタスク。</returns>
    public virtual async Task EndDepositAsync(DepositAction action)
    {
        lock (stateLock)
        {
            if (!IsFixed)
            {
                throw new DeviceException("Invalid call sequence: FixDeposit must be called before EndDeposit.", DeviceErrorCode.Illegal);
            }

            if (IsBusy)
            {
                throw new DeviceException("Device is busy", DeviceErrorCode.Busy);
            }

            IsBusy = true;
            LastErrorCode = DeviceErrorCode.Success;
            LastErrorCodeExtended = 0;
        }

        lock (stateLock)
        {
            if (!disposed)
            {
                ((Subject<Unit>)Changed).OnNext(Unit.Default);
            }
        }

        // Task.Yield() removed for determinism.
        if (depositCts != null)
        {
            await depositCts.CancelAsync().ConfigureAwait(false);
            depositCts.Dispose();
        }

        depositCts = new CancellationTokenSource();
        var token = depositCts.Token;

        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(configProvider.Config.Simulation.DepositDelayMs), timeProvider, token).ConfigureAwait(false);

            lock (stateLock)
            {
                if (action == DepositAction.Repay)
                {
                    if (logger != null)
                    {
                        logger.ZLogInformation($"Deposit Repay: Returning cash from escrow.");
                    }

                    inventory.ClearEscrow();
                }
                else if (action == DepositAction.Change)
                {
                    decimal changeAmount = Math.Max(0, DepositAmount - RequiredAmount);
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
                else
                {
                    // NoChange (or None)
                    if (logger != null)
                    {
                        logger.ZLogInformation($"Deposit NoChange: Storing all cash into inventory.");
                    }

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

                DepositStatus = DeviceDepositStatus.End;
                IsPaused = false;
                IsFixed = false;

                if (action == DepositAction.Repay)
                {
                    DepositAmount = 0m;
                    depositCounts.Clear();
                }

                inventory.ClearEscrow();
            }
        }
        catch (OperationCanceledException)
        {
            lock (stateLock)
            {
                LastErrorCode = DeviceErrorCode.Cancelled;
            }
        }
        catch (DeviceException dex)
        {
            /* Stryker disable all */
            if (logger != null)
            {
                logger.ZLogError(dex, $"EndDeposit failed with device error.");
            }

            /* Stryker restore all */
            lock (stateLock)
            {
                LastErrorCode = dex.ErrorCode;
                LastErrorCodeExtended = dex.ErrorCodeExtended;
                if (!disposed)
                {
                    ((Subject<DeviceErrorEventArgs>)ErrorEvents).OnNext(new DeviceErrorEventArgs(LastErrorCode, LastErrorCodeExtended, DeviceErrorLocus.Output, DeviceErrorResponse.Retry));
                }
            }
        }
        catch (Exception ex)
        {
            /* Stryker disable all */
            if (logger != null)
            {
                logger.ZLogError(ex, $"EndDeposit failed with unexpected error.");
            }

            /* Stryker restore all */
            lock (stateLock)
            {
                LastErrorCode = DeviceErrorCode.Failure;
                LastErrorCodeExtended = 0;
                if (!disposed)
                {
                    ((Subject<DeviceErrorEventArgs>)ErrorEvents).OnNext(new DeviceErrorEventArgs(LastErrorCode, 0, DeviceErrorLocus.Output, DeviceErrorResponse.Retry));
                }
            }
        }
        finally
        {
            lock (stateLock)
            {
                IsBusy = false;
                if (!disposed)
                {
                    ((Subject<Unit>)Changed).OnNext(Unit.Default);
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
            needsFix = !IsFixed;
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
            if (IsPaused == requestedPause)
            {
                throw new DeviceException($"Device is already {(requestedPause ? "paused" : "running")}.", DeviceErrorCode.Illegal);
            }

            IsPaused = requestedPause;
        }

        lock (stateLock)
        {
            if (!disposed)
            {
                ((Subject<Unit>)Changed).OnNext(Unit.Default);
            }
        }
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
            if (!ValidateTrackingPreconditions())
            {
                return;
            }

            var config = configProvider.Config;

            foreach (var kv in counts)
            {
                ProcessDenominationTracking(kv.Key, kv.Value, config);
            }

            NotifyTrackingEvents();
        }
    }

    /// <summary>指定された金額に近い金種をリジェクト庫（返却用）に投入します。</summary>
    /// <param name="amount">リジェクトする合計金額。</param>
    public void TrackReject(decimal amount)
    {
        lock (stateLock)
        {
            if (!IsDepositInProgress || IsPaused)
            {
                return;
            }

            RejectAmount += amount;
            if (RealTimeDataEnabled && !disposed)
            {
                ((Subject<DeviceDataEventArgs>)DataEvents).OnNext(new DeviceDataEventArgs(0));
            }

            if (!disposed)
            {
                ((Subject<Unit>)Changed).OnNext(Unit.Default);
            }
        }
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
            internalConfigProvider?.Dispose();
        }
    }

    private bool ValidateTrackingPreconditions()
    {
        if (DepositStatus != DeviceDepositStatus.Counting || IsPaused)
        {
            return false;
        }

        if (IsFixed)
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

        return true;
    }

    private void ProcessDenominationTracking(DenominationKey key, int count, CashChangerSimulator.Core.Configuration.SimulatorConfiguration config)
    {
        lock (stateLock)
        {
            // [UPOS] Simulate identification/validation
            DepositStatus = DeviceDepositStatus.Validation;

            // [CAPACITY] Check for overflow
            var denomConfig = config.GetDenominationSetting(key);
            var maxCount = denomConfig.Full;

            var currentInStorage = inventory.GetCount(key);
            var available = Math.Max(0, maxCount - currentInStorage);
            var overflow = Math.Max(0, count - available);

            // [LIFECYCLE] Record progress
            if (depositCounts.TryGetValue(key, out var current))
            {
                depositCounts[key] = current + count;
            }
            else
            {
                depositCounts[key] = count;
            }

            inventory.AddEscrow(key, count);
            DepositAmount += key.Value * count;
            OverflowAmount += key.Value * overflow;

            for (var i = 0; i < count; i++)
            {
                depositedSerials.Add($"SN-{key.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}-{Guid.NewGuid().ToString()[..8]}");
            }

            // [LIFECYCLE] Finished identification
            DepositStatus = DeviceDepositStatus.Counting;
        }
    }

    private void NotifyTrackingEvents()
    {
        if (RealTimeDataEnabled && !disposed)
        {
            ((Subject<DeviceDataEventArgs>)DataEvents).OnNext(new DeviceDataEventArgs(0));
        }

        if (!disposed)
        {
            ((Subject<Unit>)Changed).OnNext(Unit.Default);
        }
    }
}
