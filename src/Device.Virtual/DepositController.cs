using System.Diagnostics.CodeAnalysis;
using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using PosSharp.Abstractions;
using Microsoft.Extensions.Logging;
using R3;
using ZLogger;

namespace CashChangerSimulator.Device.Virtual;

/// <summary>入金シーケンスのライフサイクルを管理するコントローラー(仮想デバイス実装)。</summary>
public class DepositController : IDisposable
{
    private readonly Inventory inventory;
    private readonly HardwareStatusManager hardwareStatusManager;
    private readonly ConfigurationProvider configProvider;
    private readonly bool isConfigInternal;
    private readonly TimeProvider timeProvider;
    /* Stryker disable all */
    private readonly ILogger<DepositController>? logger = LogProvider.CreateLogger<DepositController>();
    /* Stryker restore all */
    private readonly CompositeDisposable disposables = [];
    private readonly Lock stateLock = new();
    private readonly DepositState state = new();
    private readonly DepositCalculator calculator;
    private readonly DepositTracker tracker;
    private bool disposed;

    /// <summary>DepositController クラスの新しいインスタンスを初期化します。</summary>
    /// <param name="inventory">在庫管理モデル。</param>
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
        this.configProvider = configProvider ?? new ConfigurationProvider();
        /* Stryker disable once all : Boilerplate config ownership logic */
        this.isConfigInternal = configProvider == null;
        this.timeProvider = timeProvider ?? TimeProvider.System;

        calculator = new DepositCalculator(logger, this.inventory, manager);
        tracker = new DepositTracker(this.inventory, this.configProvider);
    }

    /// <summary>状態が変更されたときに通知されるストリーム。</summary>
    public virtual Observable<Unit> Changed => tracker.Changed;

    /// <summary>データイベントの通知ストリーム。</summary>
    public virtual Observable<PosSharp.Abstractions.UposDataEventArgs> DataEvents => tracker.DataEvents;

    /// <summary>エラーイベントの通知ストリーム。</summary>
    public virtual Observable<PosSharp.Abstractions.UposErrorEventArgs> ErrorEvents => tracker.ErrorEvents;

    /// <summary>リアルタイムデータの通知。上位層(アダプター等)が利用します。</summary>
    public bool RealTimeDataEnabled { get; set; }

    /// <summary>投入された合計金額を取得します。</summary>
    public virtual decimal DepositAmount
    {
        get
        {
            lock (stateLock)
            {
                return state.DepositAmount;
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
                return state.OverflowAmount;
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
                return state.RejectAmount;
            }
        }
    }

    /// <summary>投入された各種金種の枚数を取得します。</summary>
    public virtual IReadOnlyDictionary<DenominationKey, int> DepositCounts
    {
        get
        {
            // Dictionaryのコピーが必要なため、ここだけはLockを使用。
            // ただし、もしここでもデッドロックが起きる場合はスナップショット方式を検討。
            lock (stateLock)
            {
                return new Dictionary<DenominationKey, int>(state.Counts);
            }
        }
    }

    /// <summary>現在の預入状態を取得します。</summary>
    public virtual DeviceDepositStatus DepositStatus
    {
        get => state.Status;
        private set
        {
            lock (stateLock)
            {
                state.Status = value;
            }
        }
    }

    /// <summary>入金処理が進行中かどうかを取得します。</summary>
    public virtual bool IsDepositInProgress => state.Status is
        DeviceDepositStatus.Start or
        DeviceDepositStatus.Counting or
        DeviceDepositStatus.Validation;

    /// <summary>入金処理が一時停止中かどうかを取得します。</summary>
    public virtual bool IsPaused
    {
        get
        {
            lock (stateLock)
            {
                return state.IsPaused;
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
                return state.IsFixed;
            }
        }
    }

    /// <summary>デバイスがビジー状態かどうかを取得します。</summary>
    public virtual bool IsBusy
    {
        get
        {
            /* Stryker disable once all : Thread-safety lock guard */
            lock (stateLock)
            {
                return state.IsBusy;
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
                return state.LastErrorCode;
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
                return state.LastErrorCodeExtended;
            }
        }
        private set
        {
            lock (stateLock)
            {
                state.LastErrorCodeExtended = value;
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
                return [.. state.LastDepositedSerials];
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
                return state.RequiredAmount;
            }
        }

        set
        {
            bool hasChanged = false;
            lock (stateLock)
            {
                if (state.RequiredAmount == value)
                {
                    return;
                }

                state.RequiredAmount = value;
                hasChanged = true;
            }

            if (hasChanged && !disposed)
            {
                tracker.NotifyChanged();
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
            logger?.ZLogInformation($"BeginDeposit called. Current Status: {state.Status}");

            /* Stryker restore all */

            if (state.IsBusy)
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

            state.Reset();
            DepositStatus = DeviceDepositStatus.Counting;
            inventory.ClearEscrow();
        }

        if (!disposed)
        {
            tracker.NotifyChanged();
        }
    }

    /// <summary>投入された金額を確定させます。</summary>
    public virtual void FixDeposit()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        lock (stateLock)
        {
            if (state.Status != DeviceDepositStatus.Counting)
            {
                throw new DeviceException("Counting is not in progress.", DeviceErrorCode.Illegal);
            }

            state.IsFixed = true;
            state.LastDepositedSerials.Clear();
            state.LastDepositedSerials.AddRange(state.DepositedSerials);
        }

        // Stryker disable once all : Thread-safety guard
        if (!disposed)
        {
            tracker.NotifyChanged();
        }
    }

    /// <summary>入金処理を非同期で終了します。</summary>
    /// <param name="action">終了時のアクション(収納または返却)。</param>
    /// <returns>完了を示すタスク。</returns>
    public virtual async Task EndDepositAsync(DepositAction action)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        PrepareEndDeposit();

        /* Stryker disable once all : Thread-safety guard */
        if (!disposed)
        {
            tracker.NotifyChanged();
        }

        await tracker.CancelCurrentAsync().ConfigureAwait(false);
        var token = tracker.CreateNewToken();

        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(configProvider.Config.Simulation.DepositDelayMs), timeProvider, token).ConfigureAwait(false);
            PerformDepositAction(action);
        }
        catch (OperationCanceledException)
        {
            HandleEndDepositCancellation();
        }
        catch (DeviceException dex)
        {
            HandleEndDepositDeviceException(dex);
        }
        catch (Exception ex)
        {
            HandleEndDepositUnexpectedException(ex);
        }
        finally
        {
            FinalizeEndDeposit();
        }
    }

    private void PrepareEndDeposit()
    {
        lock (stateLock)
        {
            if (!state.IsFixed)
            {
                throw new DeviceException("Invalid call sequence: FixDeposit must be called before EndDeposit.", DeviceErrorCode.Illegal);
            }

            if (state.IsBusy)
            {
                throw new DeviceException("Device is busy", DeviceErrorCode.Busy);
            }

            state.IsBusy = true;
            state.LastErrorCode = DeviceErrorCode.Success;
            LastErrorCodeExtended = 0;
        }
    }

    private void PerformDepositAction(DepositAction action)
    {
        lock (stateLock)
        {
            if (action == DepositAction.Repay)
            {
                calculator.ProcessRepay();
            }
            else if (action == DepositAction.Change)
            {
                calculator.ProcessChange(state.DepositAmount, state.RequiredAmount, state.Counts);
            }
            else
            {
                calculator.ProcessNoChange(state.Counts);
            }

            if (action != DepositAction.Repay && hardwareStatusManager.IsOverlapped.CurrentValue)
            {
                throw new DeviceException("Device Error (Overlap). Cannot complete deposit.", DeviceErrorCode.Overlapped);
            }

            DepositStatus = DeviceDepositStatus.End;
            state.IsPaused = false;
            state.IsFixed = false;

            if (action == DepositAction.Repay)
            {
                state.DepositAmount = 0m;
                state.Counts.Clear();
            }

            inventory.ClearEscrow();
        }
    }

    private void HandleEndDepositCancellation()
    {
        lock (stateLock)
        {
            state.LastErrorCode = DeviceErrorCode.Cancelled;
        }
    }

    private void HandleEndDepositDeviceException(DeviceException dex)
    {
        /* Stryker disable once all : Mutation causes CS1620 in ZLogger call */
        logger?.ZLogError(dex, $"EndDeposit failed with device error.");

        lock (stateLock)
        {
            state.LastErrorCode = dex.ErrorCode;
            LastErrorCodeExtended = dex.ErrorCodeExtended;
        }

        /* Stryker disable once all : Thread-safety guard */
        if (!disposed)
        {
            tracker.NotifyError(dex.ErrorCode, dex.ErrorCodeExtended);
        }
    }

    private void HandleEndDepositUnexpectedException(Exception ex)
    {
        /* Stryker disable once all : Mutation causes CS1620 in ZLogger call */
        logger?.ZLogError(ex, $"EndDeposit failed with unexpected error.");

        lock (stateLock)
        {
            state.LastErrorCode = DeviceErrorCode.Failure;
            LastErrorCodeExtended = 0;
        }

        /* Stryker disable once all : Thread-safety guard */
        if (!disposed)
        {
            tracker.NotifyError(DeviceErrorCode.Failure, 0);
        }
    }

    private void FinalizeEndDeposit()
    {
        lock (stateLock)
        {
            state.IsBusy = false;
        }

        /* Stryker disable once all : Thread-safety guard */
        if (!disposed)
        {
            tracker.NotifyChanged();
        }

        tracker.ResetToken();
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
        if (state.LastErrorCode != DeviceErrorCode.Success)
        {
            throw new DeviceException("RepayDeposit failed.", state.LastErrorCode, state.LastErrorCodeExtended);
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
            if (state.IsPaused == requestedPause)
            {
                throw new DeviceException($"Device is already {(requestedPause ? "paused" : "running")}.", DeviceErrorCode.Illegal);
            }

            state.IsPaused = requestedPause;
        }

        // Stryker disable once all : Thread-safety check
        if (!disposed)
        {
            tracker.NotifyChanged();
        }
    }

    /// <summary>単一の金種の投入を追跡します。</summary>
    /// <param name="key">金種。</param>
    /// <param name="count">枚数。</param>
    public void TrackDeposit(DenominationKey key, int count = 1)
    {
        /* Stryker disable once all : Redundant guard (TrackBulkDeposit handles this) */
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

            foreach (var kv in counts)
            {
                tracker.ProcessDenominationTracking(kv.Key, kv.Value, state);
            }
        }

        NotifyTrackingEvents();
    }

    /// <summary>指定された金額に近い金種をリジェクト庫(返却用)に投入します。</summary>
    /// <param name="amount">リジェクトする合計金額。</param>
    public void TrackReject(decimal amount)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        lock (stateLock)
        {
            if (!IsDepositInProgress || state.IsPaused)
            {
                return;
            }

            state.RejectAmount += amount;
        }

        if (RealTimeDataEnabled && !disposed)
        {
            tracker.NotifyData(0);
        }

        // Stryker disable once all : Thread-safety check
        if (!disposed)
        {
            tracker.NotifyChanged();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);

        /* Stryker disable once Statement : Finalizer suppression */
        GC.SuppressFinalize(this);
    }

    /// <summary>リソースを破棄します。</summary>
    /// <param name="disposing">マネージリソースを破棄する場合は true。</param>
    protected virtual void Dispose(bool disposing)
    {
        lock (stateLock)
        {
            /* Stryker disable once all : Boilerplate guard */
            if (disposed)
            {
                return;
            }

            disposed = true;
        }

        if (disposing)
        {
            tracker.CancelCurrent();
            tracker.Dispose();
            disposables.Dispose();

            /* Stryker disable all : Boilerplate disposal logic */
            if (isConfigInternal)
            {
                configProvider.Dispose();
            }
            /* Stryker restore all */
        }
    }

    private bool ValidateTrackingPreconditions()
    {
        if (state.Status != DeviceDepositStatus.Counting || state.IsPaused)
        {
            return false;
        }

        if (state.IsFixed)
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

    private void NotifyTrackingEvents()
    {
        // Stryker disable once Logical : Data event state combination block
        if (RealTimeDataEnabled && !disposed)
        {
            tracker.NotifyData(0);
        }

        /* Stryker disable once all : Thread-safety guard */
        if (!disposed)
        {
            tracker.NotifyChanged();
        }
    }
}
