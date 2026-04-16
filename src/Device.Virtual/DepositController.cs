using System.Diagnostics.CodeAnalysis;
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

/// <summary>入金シーケンスのライフサイクルを管理するコントローラー(仮想デバイス実装)。</summary>
/// <param name="inventory">在庫管理モデル。</param>
/// <param name="hardwareStatusManager">ハードウェア状態管理。</param>
/// <param name="manager">釣銭機マネージャー。</param>
/// <param name="configProvider">設定プロバイダー。</param>
/// <param name="timeProvider">時間プロバイダー。</param>
public class DepositController(
    Inventory inventory,
    HardwareStatusManager? hardwareStatusManager = null,
    CashChangerManager? manager = null,
    ConfigurationProvider? configProvider = null,
    TimeProvider? timeProvider = null) : IDisposable
{
    private readonly Inventory inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
    private readonly HardwareStatusManager hardwareStatusManager = hardwareStatusManager ?? HardwareStatusManager.Create();
    private readonly ConfigurationProvider configProvider = configProvider ?? new ConfigurationProvider();
    private readonly bool isConfigInternal = configProvider == null;
    private readonly CashChangerManager? manager = manager;
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;
    private readonly ILogger<DepositController>? logger = LogProvider.CreateLogger<DepositController>();
    private readonly CompositeDisposable disposables = [];
    private readonly Lock stateLock = new();
    private readonly Dictionary<DenominationKey, int> depositCounts = [];
    private readonly List<string> depositedSerials = [];
    private readonly List<string> lastDepositedSerials = [];
    private readonly Subject<Unit> changedSubject = new();
    private readonly Subject<DeviceDataEventArgs> dataEventsSubject = new();
    private readonly Subject<DeviceErrorEventArgs> errorEventsSubject = new();
    private CancellationTokenSource? depositCts;
    private bool disposed;

    /// <summary>状態が変更されたときに通知されるストリーム。</summary>
    public virtual Observable<Unit> Changed => changedSubject;

    /// <summary>データイベントの通知ストリーム。</summary>
    public virtual Observable<DeviceDataEventArgs> DataEvents => dataEventsSubject;

    /// <summary>エラーイベントの通知ストリーム。</summary>
    public virtual Observable<DeviceErrorEventArgs> ErrorEvents => errorEventsSubject;

    /// <summary>リアルタイムデータの通知。上位層(アダプター等)が利用します。</summary>
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

    /// <summary>オーバーフロー(満杯等により収納不可)した金額を取得します。</summary>
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

    /// <summary>リジェクト(偽札、汚れ等により返却)された金額を取得します。</summary>
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

    /// <summary>入金が確定(Fixed)されたかどうかを取得します。</summary>
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

    /// <summary>預入(Deposit)処理を開始します。</summary>
    public virtual void BeginDeposit()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        lock (stateLock)
        {
            /* Stryker disable all */
            logger?.ZLogInformation($"BeginDeposit called. Current Status: {DepositStatus}");

            /* Stryker restore all */

            if (IsBusy)
            {
                throw new DeviceException("Device is busy", DeviceErrorCode.Busy);
            }

            if (hardwareStatusManager.IsJammed.CurrentValue)
            {
                throw new DeviceException("Device is jammed. Cannot begin deposit.", DeviceErrorCode.Jammed);
            }

            if (hardwareStatusManager.IsOverlapped.CurrentValue)
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
        ObjectDisposedException.ThrowIf(disposed, this);
        lock (stateLock)
        {
            if (DepositStatus != DeviceDepositStatus.Counting)
            {
                throw new DeviceException("Counting is not in progress.", DeviceErrorCode.Illegal);
            }

            IsFixed = true;
            lastDepositedSerials.Clear();
            lastDepositedSerials.AddRange(depositedSerials);

            // Stryker disable once Logical : Thread-safety guard
            if (!disposed)
            {
                ((Subject<Unit>)Changed).OnNext(Unit.Default);
            }
        }
    }

    /// <summary>入金処理を非同期で終了します。</summary>
    /// <param name="action">終了時のアクション(収納または返却)。</param>
    /// <returns>完了を示すタスク。</returns>
    public virtual async Task EndDepositAsync(DepositAction action)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
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
            /* Stryker disable all : Thread-safety guard */
            if (!disposed)
            {
                // Stryker disable once Statement : State change notification during deposit
                ((Subject<Unit>)Changed).OnNext(Unit.Default);
            }
        }

        // Stryker disable all : Cancellation side effects are hard to verify deterministically in this context
        if (depositCts != null)
        {
            await depositCts.CancelAsync().ConfigureAwait(false);
            depositCts.Dispose();
        }

        /* Stryker restore all */

        depositCts = new CancellationTokenSource();
        var token = depositCts.Token;

        try
        {
            // Stryker disable once Boolean : Simulation delay
            await Task.Delay(TimeSpan.FromMilliseconds(configProvider.Config.Simulation.DepositDelayMs), timeProvider, token).ConfigureAwait(false);

            lock (stateLock)
            {
                if (action == DepositAction.Repay)
                {
                    /* Stryker disable all */
                    logger?.ZLogInformation($"Deposit Repay: Returning cash from escrow.");

                    /* Stryker restore all */

                    inventory.ClearEscrow();
                }
                else if (action == DepositAction.Change)
                {
                    decimal changeAmount = Math.Max(0, DepositAmount - RequiredAmount);
                    var storeCounts = new Dictionary<DenominationKey, int>(depositCounts);

                    /* Stryker disable all : Trace logging is non-functional */
                    logger?.ZLogTrace($"EndDepositAsync: {DepositAmount} - {RequiredAmount} = {changeAmount}");

                    /* Stryker restore all */

                    // Stryker disable once Equality : Behaviorally equivalent to >= 0 here as 0 hits else branch which also clears escrow
                    if (changeAmount > 0)
                    {
                        var availableInEscrow = inventory.EscrowCounts.OrderByDescending(kv => kv.Key.Value).ToList();
                        decimal remainingChange = changeAmount;
                        foreach (var (key, countInEscrow) in availableInEscrow)
                        {
                            // Stryker disable once Equality : Boundary calculation
                            if (remainingChange <= 0)
                            {
                                break;
                            }

                            int useCount = (int)Math.Min(countInEscrow, Math.Floor(remainingChange / key.Value));

                            // Stryker disable once Equality : Boundary calculation
                            if (useCount > 0)
                            {
                                storeCounts[key] -= useCount;
                                remainingChange -= key.Value * useCount;
                            }
                        }

                        inventory.ClearEscrow();
                        foreach (var kv in storeCounts)
                        {
                            // Stryker disable once Equality, Boolean : Defensive bounds check
                            if (kv.Value > 0)
                            {
                                inventory.AddEscrow(kv.Key, kv.Value);
                            }
                        }

                        // Stryker disable once Equality : Boundary calculation
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
                            // Stryker disable once Equality : Boundary calculation
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
                    /* Stryker disable all */
                    logger?.ZLogInformation($"Deposit NoChange: Storing all cash into inventory.");

                    /* Stryker restore all */

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
                            // Stryker disable once Equality : Boundary calculation
                            if (kv.Value > 0)
                            {
                                inventory.Add(kv.Key, kv.Value);
                            }
                        }
                    }
                }

                if (action != DepositAction.Repay && hardwareStatusManager.IsOverlapped.CurrentValue)
                {
                    // Stryker disable once String : Exception message is swallowed and only logged
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
            logger?.ZLogError(dex, $"EndDeposit failed with device error.");

            /* Stryker restore all */
            lock (stateLock)
            {
                LastErrorCode = dex.ErrorCode;
                LastErrorCodeExtended = dex.ErrorCodeExtended;

                // Stryker disable once all : Thread-safety check
                if (!disposed)
                {
                    ((Subject<DeviceErrorEventArgs>)ErrorEvents).OnNext(new DeviceErrorEventArgs(LastErrorCode, LastErrorCodeExtended, DeviceErrorLocus.Output, DeviceErrorResponse.Retry));
                }
            }
        }
        catch (Exception ex)
        {
            /* Stryker disable all */
            logger?.ZLogError(ex, $"EndDeposit failed with unexpected error.");

            /* Stryker restore all */
            lock (stateLock)
            {
                LastErrorCode = DeviceErrorCode.Failure;
                LastErrorCodeExtended = 0;

                // Stryker disable once all : Thread-safety check
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

                // Stryker disable once Logical : Thread-safety check
                if (!disposed)
                {
                    ((Subject<Unit>)Changed).OnNext(Unit.Default);
                }
            }
        }
    }

    /// <summary>入金を終了します(同期ラッパー)。</summary>
    /// <param name="action">終了時のアクション(収納または返却)。</param>
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

    /// <summary>投入された現金を返却し、入金セッションを終了します(同期ラッパー)。</summary>
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
        ObjectDisposedException.ThrowIf(disposed, this);
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
            // Stryker disable once all : Thread-safety check
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
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(key);
        TrackBulkDeposit(new Dictionary<DenominationKey, int> { { key, count } });
    }

    /// <summary>複数の金種の投入を一括で追跡します。</summary>
    /// <param name="counts">金種と枚数のセット。</param>
    public void TrackBulkDeposit(IReadOnlyDictionary<DenominationKey, int> counts)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
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

    /// <summary>指定された金額に近い金種をリジェクト庫(返却用)に投入します。</summary>
    /// <param name="amount">リジェクトする合計金額。</param>
    public void TrackReject(decimal amount)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
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

            // Stryker disable once all : Thread-safety check
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

        // Stryker disable once Statement : Finalizer suppression
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

            changedSubject.Dispose();
            dataEventsSubject.Dispose();
            errorEventsSubject.Dispose();
            disposables.Dispose();

            if (isConfigInternal)
            {
                configProvider.Dispose();
            }
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

        if (hardwareStatusManager.IsJammed.CurrentValue)
        {
            throw new DeviceException("Device is jammed during tracking.", DeviceErrorCode.Jammed);
        }

        if (hardwareStatusManager.IsOverlapped.CurrentValue)
        {
            throw new DeviceException("Device has overlapped cash. Cannot track deposit.", DeviceErrorCode.Overlapped);
        }

        return true;
    }

    private void ProcessDenominationTracking(DenominationKey key, int count, SimulatorConfiguration config)
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
        // Stryker disable once Logical : Data event state combination block
        if (RealTimeDataEnabled && !disposed)
        {
            ((Subject<DeviceDataEventArgs>)DataEvents).OnNext(new DeviceDataEventArgs(0));
        }

        // Stryker disable once Logical : Thread-safety check
        if (!disposed)
        {
            ((Subject<Unit>)Changed).OnNext(Unit.Default);
        }
    }
}
