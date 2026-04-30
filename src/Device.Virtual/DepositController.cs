using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using PosSharp.Abstractions;
using PosSharp.Core;
using Microsoft.Extensions.Logging;
using R3;
using ZLogger;

using System.Collections.Immutable;

namespace CashChangerSimulator.Device.Virtual;

/// <summary>入金シーケンスのライフサイクルを管理するコントローラー(仮想デバイス実装)。</summary>
public class DepositController : IDisposable
{
    private readonly Inventory inventory;
    private readonly HardwareStatusManager hardwareStatusManager;
    private readonly ConfigurationProvider configProvider;
    private readonly TimeProvider timeProvider;
    /* Stryker disable all */
    private readonly ILogger logger;
    private readonly AtomicState<DepositState> atomicState = new(DepositState.Empty);
    private readonly DepositTracker tracker;
    private readonly DepositCalculator calculator;
    private readonly CompositeDisposable disposables = [];
    private readonly bool isConfigInternal;
    private volatile bool disposed;

    /// <summary>初期化します。</summary>
    /// <param name="manager">マネージャー。</param>
    /// <param name="inventory">在庫管理モデル。</param>
    /// <param name="hardwareStatusManager">ハードウェア状態管理。</param>
    /// <param name="configProvider">設定プロバイダー。</param>
    /// <param name="loggerFactory">ロガーファクトリ。</param>
    /// <param name="timeProvider">時間プロバイダー。</param>
    public DepositController(
        CashChangerManager manager,
        Inventory inventory,
        HardwareStatusManager hardwareStatusManager,
        ConfigurationProvider configProvider,
        ILoggerFactory loggerFactory,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(manager);
        this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        this.hardwareStatusManager = hardwareStatusManager ?? throw new ArgumentNullException(nameof(hardwareStatusManager));
        this.configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger(nameof(DepositController));
        this.calculator = new DepositCalculator(logger, inventory, manager);
        this.tracker = new DepositTracker(inventory, configProvider);
        this.isConfigInternal = false;
    }

    /// <summary>状態が変更されたときに通知されるストリーム。</summary>
    public Observable<Unit> Changed => tracker.Changed;

    /// <summary>入金完了イベントを受け取るためのストリーム。</summary>
    public Observable<UposDataEventArgs> DataEvents => tracker.DataEvents;

    /// <summary>エラーイベントを受け取るためのストリーム。</summary>
    public Observable<UposErrorEventArgs> ErrorEvents => tracker.ErrorEvents;

    /// <summary>リアルタイムデータ更新が有効かどうかを取得します。</summary>
    public bool RealTimeDataEnabled { get; set; }

    /// <summary>現在投入されている合計金額を取得します。</summary>
    public decimal DepositAmount => atomicState.Current.DepositAmount;

    /// <summary>オーバーフロー(収納庫満杯)により返却される金額を取得します。</summary>
    public decimal OverflowAmount => atomicState.Current.OverflowAmount;

    /// <summary>リジェクト(偽札、汚れ等により返却)された金額を取得します。</summary>
    public decimal RejectAmount => atomicState.Current.RejectAmount;

    /// <summary>投入された各種金種の枚数を取得します。</summary>
    public IReadOnlyDictionary<DenominationKey, int> DepositCounts => new Dictionary<DenominationKey, int>(atomicState.Current.Counts);

    /// <summary>投入された紙幣のシリアル番号リストを取得します。</summary>
    public IReadOnlyList<string> DepositedSerials => atomicState.Current.DepositedSerials;

    /// <summary>確定時に同期される直前のシリアル番号リストを取得します。</summary>
    public IReadOnlyList<string> LastDepositedSerials => atomicState.Current.LastDepositedSerials;

    /// <summary>現在の預入状態を取得します。</summary>
    public DeviceDepositStatus DepositStatus => atomicState.Current.Status;

    /// <summary>入金処理が進行中かどうかを取得します。</summary>
    public bool IsDepositInProgress => atomicState.Current.Status is
        DeviceDepositStatus.Start or
        DeviceDepositStatus.Counting or
        DeviceDepositStatus.Validation;

    /// <summary>入金処理が一時停止中かどうかを取得します。</summary>
    public bool IsPaused => atomicState.Current.IsPaused;

    /// <summary>入金が確定(Fixed)されたかどうかを取得します。</summary>
    public bool IsFixed => atomicState.Current.IsFixed;

    /// <summary>デバイスがビジー状態かどうかを取得します。</summary>
    public bool IsBusy => atomicState.Current.IsBusy;

    /// <summary>直近に発生したエラーコードを取得します。</summary>
    public DeviceErrorCode LastErrorCode => atomicState.Current.LastErrorCode;

    /// <summary>直近に発生した拡張エラーコードを取得します。</summary>
    public int LastErrorCodeExtended => atomicState.Current.LastErrorCodeExtended;

    /// <summary>必要入金金額を取得または設定します。</summary>
    public decimal RequiredAmount
    {
        get => atomicState.Current.RequiredAmount;
        set
        {
            var result = atomicState.Transition(s =>
            {
                if (s.RequiredAmount == value) return s;
                return s with { RequiredAmount = value };
            });
            if (result.Changed)
            {
                tracker.NotifyChanged();
            }
        }
    }

    /// <summary>預入(Deposit)処理を開始します。</summary>
    public virtual void BeginDeposit()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        
        var result = atomicState.Transition(s =>
        {
            if (s.IsBusy) return s;
            
            // ガード条件の確認（ハードウェア状態）
            if (hardwareStatusManager.IsJammed.CurrentValue || hardwareStatusManager.IsOverlapped.CurrentValue)
            {
                return s;
            }

            return DepositState.Empty with
            {
                Status = DeviceDepositStatus.Counting,
                RequiredAmount = s.RequiredAmount
            };
        });

        if (result.NewState.IsBusy && !result.Changed)
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

        if (result.Changed)
        {
            tracker.CreateNewCts();
            inventory.ClearEscrow();
            tracker.NotifyChanged();
        }
    }

    /// <summary>投入された金額を確定させます。</summary>
    public virtual void FixDeposit()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        var result = atomicState.Transition(s =>
        {
            if (s.Status != DeviceDepositStatus.Counting) return s;

            return s with
            {
                IsFixed = true,
                LastDepositedSerials = s.DepositedSerials
            };
        });

        if (result.NewState.Status != DeviceDepositStatus.Counting)
        {
            throw new DeviceException("Counting is not in progress.", DeviceErrorCode.Illegal);
        }

        if (result.Changed)
        {
            tracker.NotifyChanged();
        }
    }

    /// <summary>入金を終了します。</summary>
    /// <param name="action">終了時のアクション(収納または返却)。</param>
    /// <returns>完了を示すタスク。</returns>
    public virtual async Task EndDepositAsync(DepositAction action)
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        PrepareEndDeposit();

        await tracker.CancelCurrentAsync().ConfigureAwait(false);
        var sessionCts = tracker.CreateNewCts();
        var token = sessionCts.Token;

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
        var result = atomicState.Transition(s =>
        {
            if (!s.IsFixed || s.IsBusy) return s;

            return s with { IsBusy = true, LastErrorCode = DeviceErrorCode.Success, LastErrorCodeExtended = 0 };
        });

        if (!result.NewState.IsFixed)
        {
            throw new DeviceException("Invalid call sequence: FixDeposit must be called before EndDeposit.", DeviceErrorCode.Illegal);
        }

        if (result.NewState.IsBusy && !result.Changed)
        {
            throw new DeviceException("Device is busy", DeviceErrorCode.Busy);
        }

        if (result.Changed)
        {
            tracker.NotifyChanged();
        }
    }

    private void PerformDepositAction(DepositAction action)
    {
        // 1. 現時点のスナップショット取得
        var current = atomicState.Current;

        // 2. 実処理（例外が発生した場合は 3 に進まず catch へ）
        if (action == DepositAction.Repay)
        {
            calculator.ProcessRepay();
        }
        else if (action == DepositAction.Change)
        {
            calculator.ProcessChange(current.DepositAmount, current.RequiredAmount, current.Counts);
        }
        else
        {
            calculator.ProcessNoChange(current.Counts);
        }

        if (action != DepositAction.Repay && hardwareStatusManager.IsOverlapped.CurrentValue)
        {
            throw new DeviceException("Device Error (Overlap). Cannot complete deposit.", DeviceErrorCode.Overlapped);
        }

        // 3. 状態確定
        var result = atomicState.Transition(s =>
        {
            var next = s with { Status = DeviceDepositStatus.End, IsPaused = false, IsFixed = false };
            if (action == DepositAction.Repay)
            {
                next = next with { DepositAmount = 0m, Counts = [] };
            }
            return next;
        });

        if (result.Changed)
        {
            tracker.NotifyChanged();
        }
    }

    private void HandleEndDepositCancellation()
    {
        var result = atomicState.Transition(s => s with { LastErrorCode = DeviceErrorCode.Cancelled });
        if (result.Changed)
        {
            tracker.NotifyChanged();
        }
    }

    private void HandleEndDepositDeviceException(DeviceException dex)
    {
        /* Stryker disable once all : Mutation causes CS1620 in ZLogger call */
        logger?.ZLogError(dex, $"EndDeposit failed with device error.");

        var result = atomicState.Transition(s => s with { LastErrorCode = dex.ErrorCode, LastErrorCodeExtended = dex.ErrorCodeExtended });
        
        if (result.Changed)
        {
            tracker.NotifyChanged();
        }

        if (!disposed)
        {
            tracker.NotifyError(dex.ErrorCode, dex.ErrorCodeExtended);
        }
    }

    private void HandleEndDepositUnexpectedException(Exception ex)
    {
        /* Stryker disable once all : Mutation causes CS1620 in ZLogger call */
        logger?.ZLogError(ex, $"EndDeposit failed with unexpected error.");

        var result = atomicState.Transition(s => s with { LastErrorCode = DeviceErrorCode.Failure, LastErrorCodeExtended = 0 });

        if (result.Changed)
        {
            tracker.NotifyChanged();
        }

        if (!disposed)
        {
            tracker.NotifyError(DeviceErrorCode.Failure, 0);
        }
    }

    private void FinalizeEndDeposit()
    {
        var result = atomicState.Transition(s => s with { IsBusy = false });
        if (result.Changed)
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
        bool needsFix = !IsFixed;

        if (needsFix)
        {
            FixDeposit();
        }

        await EndDepositAsync(DepositAction.Repay).ConfigureAwait(false);
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

        bool requestedPause = control == DeviceDepositPause.Pause;
        
        var result = atomicState.Transition(s =>
        {
            if (!IsDepositInProgress) return s;
            if (s.IsPaused == requestedPause) return s;

            return s with { IsPaused = requestedPause };
        });

        if (!IsDepositInProgress)
        {
            throw new DeviceException("Session not active.", DeviceErrorCode.Illegal);
        }

        if (result.NewState.IsPaused == requestedPause && !result.Changed)
        {
            throw new DeviceException($"Device is already {(requestedPause ? "paused" : "running")}.", DeviceErrorCode.Illegal);
        }

        if (result.Changed)
        {
            tracker.NotifyChanged();
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

        if (hardwareStatusManager.IsOverlapped.CurrentValue)
        {
            throw new DeviceException("Device has overlapped cash. Cannot track deposit.", DeviceErrorCode.Overlapped);
        }

        if (hardwareStatusManager.IsJammed.CurrentValue)
        {
            throw new DeviceException("Device is jammed during tracking.", DeviceErrorCode.Jammed);
        }

        bool changed = false;
        foreach (var kv in counts)
        {
            var result = atomicState.Transition(s =>
            {
                if (s.Status != DeviceDepositStatus.Counting || s.IsPaused || s.IsFixed)
                {
                    return s;
                }
                return tracker.ProcessDenominationTracking(kv.Key, kv.Value, s);
            });

            if (result.OldState.IsFixed)
            {
                 throw new DeviceException("Deposit is already fixed.", DeviceErrorCode.Illegal);
            }

            if (result.Changed)
            {
                changed = true;
            }
        }

        if (changed)
        {
            tracker.NotifyChanged();
            if (RealTimeDataEnabled)
            {
                tracker.NotifyData(0);
            }
        }
    }

    /// <summary>指定された金額に近い金種をリジェクト庫(返却用)に投入します。</summary>
    /// <param name="amount">リジェクトする合計金額。</param>
    public void TrackReject(decimal amount)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        
        var result = atomicState.Transition(s =>
        {
            if (!IsDepositInProgress || s.IsPaused)
            {
                return s;
            }

            return s with { RejectAmount = s.RejectAmount + amount };
        });

        if (result.Changed)
        {
            tracker.NotifyChanged();
            if (RealTimeDataEnabled)
            {
                tracker.NotifyData(0);
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>リソースを解放します。</summary>
    /// <param name="disposing">マネージリソースを解放するかどうか。</param>
    protected virtual void Dispose(bool disposing)
    {
        if (Interlocked.Exchange(ref disposed, true))
        {
            return;
        }

        if (disposing)
        {
            tracker.CancelCurrent();
            tracker.Dispose();
            disposables.Dispose();
            if (isConfigInternal) configProvider.Dispose();
        }
    }
}
