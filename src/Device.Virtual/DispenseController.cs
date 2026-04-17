using System.Diagnostics.CodeAnalysis;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Services.DeviceEventTypes;
using Microsoft.Extensions.Logging;
using R3;
using ZLogger;

namespace CashChangerSimulator.Device.Virtual;

/// <summary>出金(払出)シーケンスを管理するコントローラー(仮想デバイス実装)。</summary>
public class DispenseController : IDisposable
{
    private readonly CashChangerManager manager;
    private readonly Inventory inventory;
    private readonly ConfigurationProvider configProvider;
    private readonly HardwareStatusManager hardwareStatusManager;
    private readonly IDeviceSimulator simulator;
    private readonly ILogger<DispenseController> logger;

    private readonly Lock stateLock = new();
    private readonly DispenseState state = new();
    private readonly DispenseTracker tracker = new();
    private bool disposed;

    /// <summary>初期化します。</summary>
    /// <param name="manager">マネージャー。</param>
    /// <param name="inventory">在庫管理モデル。</param>
    /// <param name="configProvider">設定プロバイダー。</param>
    /// <param name="loggerFactory">ロガーファクトリ。</param>
    /// <param name="hardwareStatusManager">ハードウェア状態管理。</param>
    /// <param name="simulator">デバイスシミュレーター。</param>
    public DispenseController(
        CashChangerManager manager,
        Inventory inventory,
        ConfigurationProvider configProvider,
        ILoggerFactory loggerFactory,
        HardwareStatusManager hardwareStatusManager,
        IDeviceSimulator simulator)
    {
        this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
        this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        this.configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
        this.hardwareStatusManager = hardwareStatusManager ?? throw new ArgumentNullException(nameof(hardwareStatusManager));
        this.simulator = simulator ?? throw new ArgumentNullException(nameof(simulator));
        this.logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<DispenseController>();
    }

    /// <summary>状態が変更されたときに通知されるストリーム。</summary>
    public Observable<Unit> Changed => tracker.Changed;

    /// <summary>出力完了イベントを受け取るためのストリーム。</summary>
    public Observable<DeviceOutputCompleteEventArgs> OutputCompleteEvents => tracker.OutputCompleteEvents;

    /// <summary>エラーイベントを受け取るためのストリーム。</summary>
    public Observable<DeviceErrorEventArgs> ErrorEvents => tracker.ErrorEvents;

    /// <summary>現在のステータスを取得します。</summary>
    public CashDispenseStatus Status
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

    /// <summary>最後に発生したエラーコードを取得します。</summary>
    public DeviceErrorCode LastErrorCode => state.LastErrorCode;

    /// <summary>最後に発生した詳細エラーコードを取得します。</summary>
    public int LastErrorCodeExtended => state.LastErrorCodeExtended;

    /// <summary>処理中かどうかを取得します。</summary>
    public bool IsBusy => state.IsBusy;

    /// <summary>シミュレーターを取得します(テスト用)。</summary>
    public IDeviceSimulator Simulator => simulator;

    /// <summary>指定された金額を払い出します。</summary>
    /// <param name="amount">払い出す金額。</param>
    /// <param name="isRepay">返却処理かどうか。</param>
    /// <returns>タスク。</returns>
    public virtual async Task DispenseChangeAsync(int amount, bool isRepay)
    {
        lock (stateLock)
        {
            if (!hardwareStatusManager.IsConnected.CurrentValue)
            {
                throw new DeviceException("Device is not connected.", DeviceErrorCode.Closed);
            }
        }

        var counts = ChangeCalculator.Calculate(inventory, amount, null, filter: k =>
        {
            var setting = configProvider.Config.GetDenominationSetting(k);
            return setting.IsRecyclable;
        });

        // Stryker disable once Boolean : Synchronization context optimization is untestable
        await DispenseCashAsync(counts, isRepay).ConfigureAwait(false);
    }

    /// <summary>指定された金種と枚数を払い出します。</summary>
    /// <param name="dispenseCounts">払い出す金種と枚数の辞書。</param>
    /// <param name="isRepay">返却処理かどうか。</param>
    /// <returns>タスク。</returns>
    public virtual async Task DispenseCashAsync(IReadOnlyDictionary<DenominationKey, int> dispenseCounts, bool isRepay)
    {
        lock (stateLock)
        {
            ObjectDisposedException.ThrowIf(disposed, this);

            if (state.IsBusy)
            {
                throw new DeviceException("Already processing another dispense.", DeviceErrorCode.Busy);
            }

            if (!hardwareStatusManager.IsConnected.CurrentValue)
            {
                throw new DeviceException("Device is not connected.", DeviceErrorCode.Closed);
            }

            if (hardwareStatusManager.IsJammed.CurrentValue)
            {
                throw new DeviceException("Hardware is jammed.", DeviceErrorCode.Jammed);
            }

            Status = CashDispenseStatus.Busy;
            state.Status = CashDispenseStatus.Busy;
            state.LastErrorCode = DeviceErrorCode.Success;
        }

        tracker.NotifyChanged();

        var token = tracker.CreateNewToken();

        try
        {
            // Stryker disable once Boolean : Synchronization context optimization is untestable
            await simulator.SimulateDispenseAsync(token).ConfigureAwait(false);

            lock (stateLock)
            {
                Status = CashDispenseStatus.Idle;
                state.Reset();
                manager.Dispense(dispenseCounts);
            }

            tracker.NotifyComplete();
        }
        catch (OperationCanceledException)
        {
            lock (stateLock)
            {
                Status = CashDispenseStatus.Idle;
                state.Status = CashDispenseStatus.Idle;
                state.LastErrorCode = DeviceErrorCode.Cancelled;
                state.LastErrorCodeExtended = 0;
            }

            tracker.NotifyError(DeviceErrorCode.Cancelled, 0);
        }
        catch (Exception ex)
        {
            /* Stryker disable all */
            logger?.ZLogError(ex, $"Dispense failed.");
            DispenseTracker.HandleDispenseError(ex, out var code, out var codeEx);
            /* Stryker restore all */

            lock (stateLock)
            {
                Status = CashDispenseStatus.Error;
                state.Status = CashDispenseStatus.Error;
                state.LastErrorCode = code;
                state.LastErrorCodeExtended = codeEx;
            }

            tracker.NotifyError(state.LastErrorCode, state.LastErrorCodeExtended);
        }
        finally
        {
            // Stryker disable once Statement : CancellationTokenSource disposal
            tracker.ResetToken();

            if (!disposed)
            {
                tracker.NotifyChanged();
            }
        }
    }

    /// <summary>出力を中止します。</summary>
    public virtual void ClearOutput()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        tracker.CancelCurrent();

        bool notifyChanged = false;
        lock (stateLock)
        {
            if (Status == CashDispenseStatus.Error)
            {
                Status = CashDispenseStatus.Idle;
                state.Reset();
                notifyChanged = true;
            }
            else if (Status == CashDispenseStatus.Busy)
            {
                // [UPOS] Stop in-progress output immediately for UI/Test consistency
                Status = CashDispenseStatus.Idle;
                state.Status = CashDispenseStatus.Idle;
                state.LastErrorCode = DeviceErrorCode.Cancelled;
                state.LastErrorCodeExtended = 0;
                notifyChanged = true;
            }
        }

        if (notifyChanged && !disposed)
        {
            tracker.NotifyChanged();
        }
    }

    /// <summary>リソースを解放します。</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>管理リソースを解放します。</summary>
    /// <param name="disposing">破棄中かどうか。</param>
    protected virtual void Dispose(bool disposing)
    {
        lock (stateLock)
        {
            // Stryker disable once all : Idempotency guard for double dispose
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
        }
    }
}
