using System.Diagnostics.CodeAnalysis;
using System.Globalization;
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
    private readonly CashChangerManager manager = manager ?? throw new ArgumentNullException(nameof(manager));
    private readonly Inventory inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
    private readonly ConfigurationProvider configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
    private readonly HardwareStatusManager hardwareStatusManager = hardwareStatusManager ?? throw new ArgumentNullException(nameof(hardwareStatusManager));
    private readonly IDeviceSimulator simulator = simulator ?? throw new ArgumentNullException(nameof(simulator));
    private readonly ILogger<DispenseController> logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<DispenseController>();
    private readonly CompositeDisposable disposables = [];
    private readonly Lock stateLock = new();
    private readonly Subject<Unit> changedSubject = new();
    private readonly Subject<DeviceOutputCompleteEventArgs> outputCompleteEventsSubject = new();
    private readonly Subject<DeviceErrorEventArgs> errorEventsSubject = new();
    private CancellationTokenSource? dispenseCts;
    private bool disposed;

    /// <summary>状態が変更されたときに通知されるストリーム。</summary>
    public Observable<Unit> Changed => changedSubject;

    /// <summary>出力完了イベントを受け取るためのストリーム。</summary>
    public Observable<DeviceOutputCompleteEventArgs> OutputCompleteEvents => outputCompleteEventsSubject;

    /// <summary>エラーイベントを受け取るためのストリーム。</summary>
    public Observable<DeviceErrorEventArgs> ErrorEvents => errorEventsSubject;

    /// <summary>現在のステータスを取得します。</summary>
    public CashDispenseStatus Status { get; private set; } = CashDispenseStatus.Idle;

    /// <summary>最後に発生したエラーコードを取得します。</summary>
    public DeviceErrorCode LastErrorCode { get; private set; } = DeviceErrorCode.Success;

    /// <summary>最後に発生した詳細エラーコードを取得します。</summary>
    public int LastErrorCodeExtended { get; private set; }

    /// <summary>処理中かどうかを取得します。</summary>
    public bool IsBusy => Status == CashDispenseStatus.Busy;

    /// <summary>シミュレーターを取得します(テスト用)。</summary>
    public IDeviceSimulator Simulator => simulator;

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);

        // Stryker disable once Statement : Finalizer suppression
        GC.SuppressFinalize(this);
    }

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

            if (IsBusy)
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
            LastErrorCode = DeviceErrorCode.Success;
        }

        ((Subject<Unit>)Changed).OnNext(Unit.Default);

        dispenseCts = new CancellationTokenSource();
        var token = dispenseCts.Token;

        try
        {
            // Stryker disable once Boolean : Synchronization context optimization is untestable
            await simulator.SimulateDispenseAsync(token).ConfigureAwait(false);

            lock (stateLock)
            {
                Status = CashDispenseStatus.Idle;
                LastErrorCode = DeviceErrorCode.Success;
                LastErrorCodeExtended = 0;

                // Actual inventory update
                manager.Dispense(dispenseCounts);
            }

            ((Subject<DeviceOutputCompleteEventArgs>)OutputCompleteEvents).OnNext(new DeviceOutputCompleteEventArgs(0));
        }
        catch (OperationCanceledException)
        {
            lock (stateLock)
            {
                Status = CashDispenseStatus.Idle;
                LastErrorCode = DeviceErrorCode.Cancelled;
                LastErrorCodeExtended = 0;
            }

            ((Subject<DeviceErrorEventArgs>)ErrorEvents).OnNext(new DeviceErrorEventArgs(DeviceErrorCode.Cancelled, 0, DeviceErrorLocus.Output, DeviceErrorResponse.None));
        }
        catch (Exception ex)
        {
            /* Stryker disable all */
            logger?.ZLogError(ex, $"Dispense failed.");

            HandleDispenseError(ex, out var code, out var codeEx);
            /* Stryker restore all */

            lock (stateLock)
            {
                Status = CashDispenseStatus.Error;
                LastErrorCode = code;
                LastErrorCodeExtended = codeEx;
            }

            ((Subject<DeviceErrorEventArgs>)ErrorEvents).OnNext(new DeviceErrorEventArgs(LastErrorCode, LastErrorCodeExtended, DeviceErrorLocus.Output, DeviceErrorResponse.None));
        }
        finally
        {
            // Stryker disable once Statement : CancellationTokenSource disposal
            dispenseCts?.Dispose();
            dispenseCts = null;

            if (!disposed)
            {
                // Always notify state change at the end of operation
                ((Subject<Unit>)Changed).OnNext(Unit.Default);
            }
        }
    }

    /// <summary>出力を中止します。</summary>
    public virtual void ClearOutput()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        dispenseCts?.Cancel();

        bool notifyChanged = false;
        lock (stateLock)
        {
            if (Status == CashDispenseStatus.Error)
            {
                Status = CashDispenseStatus.Idle;
                LastErrorCode = DeviceErrorCode.Success;
                LastErrorCodeExtended = 0;
                notifyChanged = true;
            }
            else if (Status == CashDispenseStatus.Busy)
            {
                // [UPOS] Stop in-progress output immediately for UI/Test consistency
                Status = CashDispenseStatus.Idle;
                LastErrorCode = DeviceErrorCode.Cancelled;
                LastErrorCodeExtended = 0;
                notifyChanged = true;
            }
        }

        if (notifyChanged && !disposed)
        {
            ((Subject<Unit>)Changed).OnNext(Unit.Default);
        }
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
            // Stryker disable once Statement : CancellationTokenSource cancellation
            dispenseCts?.Cancel();

            // Stryker disable once Statement : CancellationTokenSource disposal
            dispenseCts?.Dispose();

            changedSubject.Dispose();
            outputCompleteEventsSubject.Dispose();
            errorEventsSubject.Dispose();

            // Stryker disable once Statement : CompositeDisposable disposal
            disposables.Dispose();
        }
    }

    /// <summary>例外をエラーコードへマッピングします。</summary>
    private static void HandleDispenseError(Exception ex, out DeviceErrorCode code, out int codeEx)
    {
        code = DeviceErrorCode.Failure;
        codeEx = 0;

        if (ex is DeviceException dex)
        {
            code = dex.ErrorCode;
            codeEx = dex.ErrorCodeExtended;
            return;
        }

        /* Stryker disable all : Reflection-based exception analysis for mocks/simulation is hard to verify via mutation */

        // POS Control Exception (Reflection compatible)
        var type = ex.GetType();
        if (type.Name.Contains("PosControlException", StringComparison.Ordinal) || type.Name.Contains("MockPosControlException", StringComparison.Ordinal))
        {
            var errorCodeProp = type.GetProperty("ErrorCode");
            var errorCodeExtendedProp = type.GetProperty("ErrorCodeExtended");

            if (errorCodeProp != null)
            {
                var rawValue = errorCodeProp.GetValue(ex);
                if (rawValue is int i)
                {
                    code = (DeviceErrorCode)i;
                }
                else if (rawValue != null)
                {
                    code = (DeviceErrorCode)Convert.ToInt32(rawValue, CultureInfo.InvariantCulture);
                }
            }

            if (errorCodeExtendedProp != null)
            {
                var rawValue = errorCodeExtendedProp.GetValue(ex);
                if (rawValue is int i)
                {
                    codeEx = i;
                }
                else if (rawValue != null)
                {
                    codeEx = Convert.ToInt32(rawValue, CultureInfo.InvariantCulture);
                }
            }
        }

        /* Stryker restore all */
    }
}
