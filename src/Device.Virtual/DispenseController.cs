using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Services;
using PosSharp.Abstractions;
using PosSharp.Core;
using Microsoft.Extensions.Logging;
using R3;
using ZLogger;

namespace CashChangerSimulator.Device.Virtual;

/// <summary>出金(払出)シーケンスを管理するコントローラー(仮想デバイス実装)。</summary>
/// <param name="manager">マネージャー。</param>
/// <param name="inventory">在庫管理モデル。</param>
/// <param name="configProvider">設定プロバイダー。</param>
/// <param name="loggerFactory">ロガーファクトリ。</param>
/// <param name="hardwareStatusManager">ハードウェア状態管理。</param>
/// <param name="simulator">デバイスシミュレーター。</param>
public class DispenseController(
    CashChangerManager manager,
    Inventory inventory,
    ConfigurationProvider configProvider,
    ILoggerFactory loggerFactory,
    HardwareStatusManager hardwareStatusManager,
    IDeviceSimulator simulator) : IDisposable
{
    private readonly Inventory inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
    private readonly ConfigurationProvider configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
    private readonly HardwareStatusManager hardwareStatusManager = hardwareStatusManager ?? throw new ArgumentNullException(nameof(hardwareStatusManager));
    private readonly IDeviceSimulator simulator = simulator ?? throw new ArgumentNullException(nameof(simulator));
    private readonly ILogger<DispenseController> logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<DispenseController>();

    private readonly AtomicState<DispenseState> atomicState = new(new DispenseState());
    private readonly DispenseTracker tracker = new();
    private readonly DispenseCalculator calculator = new(manager ?? throw new ArgumentNullException(nameof(manager)), hardwareStatusManager);
    private volatile bool disposed;


    /// <summary>状態が変更されたときに通知されるストリーム。</summary>
    public Observable<Unit> Changed => tracker.Changed;

    /// <summary>出力完了イベントを受け取るためのストリーム。</summary>
    public Observable<UposOutputCompleteEventArgs> OutputCompleteEvents => tracker.OutputCompleteEvents;

    /// <summary>エラーイベントを受け取るためのストリーム。</summary>
    public Observable<UposErrorEventArgs> ErrorEvents => tracker.ErrorEvents;

    /// <summary>現在のステータスを取得します。</summary>
    public virtual CashDispenseStatus Status => atomicState.Current.Status;

    /// <summary>最後に発生したエラーコードを取得します。</summary>
    public DeviceErrorCode LastErrorCode => atomicState.Current.LastErrorCode;

    /// <summary>最後に発生した詳細エラーコードを取得します。</summary>
    public int LastErrorCodeExtended => atomicState.Current.LastErrorCodeExtended;

    /// <summary>処理中かどうかを取得します。</summary>
    public bool IsBusy => atomicState.Current.IsBusy;

    /// <summary>シミュレーターを取得します(テスト用)。</summary>
    public IDeviceSimulator Simulator => simulator;

    /// <summary>指定された金額を払い出します。</summary>
    /// <param name="amount">払い出す金額。</param>
    /// <param name="isRepay">返却処理かどうか。</param>
    /// <returns>タスク。</returns>
    public virtual async Task DispenseChangeAsync(
        int amount,
        bool isRepay)
    {
        if (!hardwareStatusManager.IsConnected.CurrentValue)
        {
            throw new DeviceException("Device is not connected.", DeviceErrorCode.Closed);
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
    public virtual async Task DispenseCashAsync(
        IReadOnlyDictionary<DenominationKey, int> dispenseCounts,
        bool isRepay)
    {
        PrepareDispense();

        var token = tracker.CreateNewToken();

        try
        {
            // Stryker disable once Boolean : Synchronization context optimization is untestable
            await simulator.SimulateDispenseAsync(token).ConfigureAwait(false);

            // 状態の確定
            atomicState.Transition(_ => new DispenseState(CashDispenseStatus.Idle, DeviceErrorCode.Success, 0));
            
            // 実処理(在庫反映)
            calculator.ProcessDispense(dispenseCounts, isRepay);

            tracker.NotifyComplete();
        }
        catch (OperationCanceledException)
        {
            HandleDispenseCancellation();
        }
        catch (Exception ex)
        {
            HandleDispenseException(ex);
        }
        finally
        {
            FinalizeDispense();
        }
    }

    private void PrepareDispense()
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        var result = atomicState.Transition(s =>
        {
            if (s.IsBusy) return s;
            
            // ガード条件の確認（ハードウェア状態）
            if (!hardwareStatusManager.IsConnected.CurrentValue || hardwareStatusManager.IsJammed.CurrentValue)
            {
                return s;
            }

            return s with
            {
                Status = CashDispenseStatus.Busy,
                LastErrorCode = DeviceErrorCode.Success,
                LastErrorCodeExtended = 0
            };
        });

        if (result.NewState.IsBusy && !result.Changed)
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

        if (result.Changed)
        {
            tracker.NotifyChanged();
        }
    }

    private void HandleDispenseCancellation()
    {
        var result = atomicState.Transition(s => s with
        {
            Status = CashDispenseStatus.Idle,
            LastErrorCode = DeviceErrorCode.Cancelled,
            LastErrorCodeExtended = 0
        });

        if (result.Changed)
        {
            tracker.NotifyChanged();
        }

        tracker.NotifyError(DeviceErrorCode.Cancelled, 0);
    }

    private void HandleDispenseException(Exception ex)
    {
        /* Stryker disable all */
        logger?.ZLogError(ex, $"Dispense failed.");
        DispenseTracker.HandleDispenseError(ex, out var code, out var codeEx);
        /* Stryker restore all */

        var result = atomicState.Transition(s => s with
        {
            Status = CashDispenseStatus.Error,
            LastErrorCode = code,
            LastErrorCodeExtended = codeEx
        });

        if (result.Changed)
        {
            tracker.NotifyChanged();
        }

        tracker.NotifyError(result.NewState.LastErrorCode, result.NewState.LastErrorCodeExtended);
    }

    private void FinalizeDispense()
    {
        // Stryker disable once Statement : CancellationTokenSource disposal
        tracker.ResetToken();

        if (!disposed)
        {
            tracker.NotifyChanged();
        }
    }


    /// <summary>出力を中止します。</summary>
    public virtual void ClearOutput()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        tracker.CancelCurrent();

        var result = atomicState.Transition(s =>
        {
            if (s.Status == CashDispenseStatus.Error)
            {
                return new DispenseState();
            }

            if (s.Status == CashDispenseStatus.Busy)
            {
                return s with
                {
                    Status = CashDispenseStatus.Idle,
                    LastErrorCode = DeviceErrorCode.Cancelled,
                    LastErrorCodeExtended = 0
                };
            }

            return s;
        });

        if (result.Changed)
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
        if (Interlocked.Exchange(ref disposed, true))
        {
            return;
        }

        if (disposing)
        {
            tracker.CancelCurrent();
            tracker.Dispose();
        }
    }
}
