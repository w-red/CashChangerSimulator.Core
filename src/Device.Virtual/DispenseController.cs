using System.Threading;
using CashChangerSimulator.Core;
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

/// <summary>出金（払出）シーケンスを管理するコントローラー（仮想デバイス実装）。</summary>
/// <remarks>
/// UPOS などのプラットフォーム固有の SDK に依存せず、純粋な C# ロジックとして出金プロセスをシミュレートします。
/// </remarks>
public class DispenseController : IDisposable
{
    private readonly CashChangerManager manager;
    private readonly HardwareStatusManager hardwareStatusManager;
    private readonly IDeviceSimulator simulator;
    private readonly ILogger<DispenseController> logger = LogProvider.CreateLogger<DispenseController>();
    private readonly Subject<Unit> changed = new();
    private readonly Subject<DeviceOutputCompleteEventArgs> outputCompleteEvents = new();
    private readonly Subject<DeviceErrorEventArgs> errorEvents = new();
    private readonly CompositeDisposable disposables = [];
    private readonly Lock stateLock = new();
    private CancellationTokenSource? dispenseCts;
    private CashDispenseStatus status = CashDispenseStatus.Idle;
    private DeviceErrorCode lastErrorCode = DeviceErrorCode.Success;
    private int lastErrorCodeExtended;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DispenseController"/> class.
    /// </summary>
    /// <param name="manager">釣銭機マネージャー。</param>
    /// <param name="hardwareStatusManager">ハードウェア状態管理。</param>
    /// <param name="simulator">デバイスシミュレーター。</param>
    public DispenseController(
        CashChangerManager manager,
        HardwareStatusManager? hardwareStatusManager = null,
        IDeviceSimulator? simulator = null)
    {
        this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
        this.hardwareStatusManager = hardwareStatusManager ?? new HardwareStatusManager();
        this.simulator = simulator ?? new HardwareSimulator(new ConfigurationProvider());
    }

    /// <summary>状態が変更されたときに通知されるストリーム。</summary>
    public virtual Observable<Unit> Changed => changed;

    /// <summary>出力完了イベントの通知ストリーム。</summary>
    public virtual Observable<DeviceOutputCompleteEventArgs> OutputCompleteEvents => outputCompleteEvents;

    /// <summary>エラーイベントの通知ストリーム。</summary>
    public virtual Observable<DeviceErrorEventArgs> ErrorEvents => errorEvents;

    /// <summary>現在の出金状態を取得します。</summary>
    public virtual CashDispenseStatus Status
    {
        get
        {
            lock (stateLock)
            {
                return status;
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
                return status == CashDispenseStatus.Busy;
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

    /// <summary>指定された金額を非同期で払い出します。</summary>
    /// <param name="amount">払い出す金額。</param>
    /// <param name="asyncMode">非同期実行モードかどうか。</param>
    /// <param name="currencyCode">通貨コード（任意）。</param>
    /// <returns>完了を示すタスク。</returns>
    public virtual async Task DispenseChangeAsync(int amount, bool asyncMode, string? currencyCode = null)
    {
        lock (stateLock)
        {
            if (status == CashDispenseStatus.Busy)
            {
                throw new DeviceException("Device is busy", DeviceErrorCode.Busy);
            }

            if (!hardwareStatusManager.IsConnected.Value)
            {
                throw new DeviceException("Device not connected", DeviceErrorCode.Closed);
            }

            if (hardwareStatusManager.IsJammed.Value)
            {
                throw new DeviceException("Device jammed", DeviceErrorCode.Failure);
            }

            if (hardwareStatusManager.IsOverlapped.Value)
            {
                throw new DeviceException("Device overlapped", DeviceErrorCode.Failure);
            }

            status = CashDispenseStatus.Busy;
            lastErrorCode = DeviceErrorCode.Success;
            lastErrorCodeExtended = 0;
        }

        changed.OnNext(Unit.Default);
        await Task.Yield();

        if (dispenseCts != null)
        {
            await dispenseCts.CancelAsync().ConfigureAwait(false);
            dispenseCts.Dispose();
        }

        dispenseCts = new CancellationTokenSource();
        var token = dispenseCts.Token;

        if (asyncMode)
        {
            _ = Task.Run(
                async () =>
                {
                    try
                    {
                        await ExecuteDispense(() => manager.Dispense(amount, currencyCode), token).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        logger.ZLogError(ex, $"Background dispense error: {ex.Message}");
                    }
                },
                token);
        }
        else
        {
            await ExecuteDispense(() => manager.Dispense(amount, currencyCode), token).ConfigureAwait(false);
        }
    }

    /// <summary>指定された金種と枚数を非同期で払い出します。</summary>
    /// <param name="counts">払い出す金種と枚数。</param>
    /// <param name="asyncMode">非同期実行モードかどうか。</param>
    /// <returns>完了を示すタスク。</returns>
    public virtual async Task DispenseCashAsync(IReadOnlyDictionary<DenominationKey, int> counts, bool asyncMode)
    {
        ArgumentNullException.ThrowIfNull(counts);

        lock (stateLock)
        {
            if (status == CashDispenseStatus.Busy)
            {
                throw new DeviceException("Device is busy", DeviceErrorCode.Busy);
            }

            if (!hardwareStatusManager.IsConnected.Value)
            {
                throw new DeviceException("Device not connected", DeviceErrorCode.Closed);
            }

            if (hardwareStatusManager.IsJammed.Value)
            {
                throw new DeviceException("Device jammed", DeviceErrorCode.Failure);
            }

            if (hardwareStatusManager.IsOverlapped.Value)
            {
                throw new DeviceException("Device overlapped", DeviceErrorCode.Failure);
            }

            status = CashDispenseStatus.Busy;
            lastErrorCode = DeviceErrorCode.Success;
            lastErrorCodeExtended = 0;
        }

        changed.OnNext(Unit.Default);
        await Task.Yield();

        if (dispenseCts != null)
        {
            await dispenseCts.CancelAsync().ConfigureAwait(false);
            dispenseCts.Dispose();
        }

        dispenseCts = new CancellationTokenSource();
        var token = dispenseCts.Token;

        if (asyncMode)
        {
            _ = Task.Run(
                async () =>
                {
                    try
                    {
                        await ExecuteDispense(() => manager.Dispense(counts), token).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        logger.ZLogError(ex, $"Background dispense error: {ex.Message}");
                    }
                },
                token);
        }
        else
        {
            await ExecuteDispense(() => manager.Dispense(counts), token).ConfigureAwait(false);
        }
    }

    /// <summary>現在の出金をキャンセルします。</summary>
    public virtual void ClearOutput()
    {
        dispenseCts?.Cancel();
        lock (stateLock)
        {
            if (status != CashDispenseStatus.Idle)
            {
                status = CashDispenseStatus.Idle;
                lastErrorCode = DeviceErrorCode.Cancelled;
                lastErrorCodeExtended = 0;
                changed.OnNext(Unit.Default);
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
        if (disposed)
        {
            return;
        }

        if (disposing)
        {
            // 外部から注入された dependencies (simulator, manager, hardwareStatusManager) は、
            // このクラスの Dispose で破棄すべきではありません。
            if (dispenseCts != null)
            {
                dispenseCts.Cancel();
                dispenseCts.Dispose();
            }

            disposables.Dispose();
            changed.OnCompleted();
            changed.Dispose();
            outputCompleteEvents.OnCompleted();
            outputCompleteEvents.Dispose();
            errorEvents.OnCompleted();
            errorEvents.Dispose();
        }

        disposed = true;
    }

    private async Task ExecuteDispense(Action action, CancellationToken token)
    {
        DeviceErrorCode code = DeviceErrorCode.Success;
        int codeEx = 0;
        bool isError = false;

        try
        {
            await simulator.SimulateDispenseAsync(token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            action();
        }
        catch (OperationCanceledException)
        {
            // Canceled: status back to Idle without specific error result.
        }
        catch (InsufficientCashException ex)
        {
            isError = true;
            code = DeviceErrorCode.Extended;
            codeEx = 201;
            logger.ZLogError(ex, $"Insufficient cash: {ex.Message}");
        }
        catch (DeviceException dex)
        {
            isError = true;
            code = dex.ErrorCode;
            codeEx = dex.ErrorCodeExtended;
            logger.ZLogError(dex, $"Dispense failed with device error: {dex.Message}");
        }
        catch (Exception ex)
        {
            isError = true;
            code = DeviceErrorCode.Failure;
            codeEx = 0;

            // Attempt to extract error codes from external exceptions (e.g. PosControlException in tests)
            // without adding a direct dependency on POS.NET in this virtual layer.
            var type = ex.GetType();
            var pCode = type.GetProperty("ErrorCode");
            var pCodeEx = type.GetProperty("ErrorCodeExtended");

            if (pCode != null)
            {
                var val = pCode.GetValue(ex);
                if (val != null)
                {
                    code = (DeviceErrorCode)Convert.ToInt32(val, System.Globalization.CultureInfo.InvariantCulture);
                }
            }

            if (pCodeEx != null)
            {
                var valEx = pCodeEx.GetValue(ex);
                if (valEx != null)
                {
                    codeEx = Convert.ToInt32(valEx, System.Globalization.CultureInfo.InvariantCulture);
                }
            }

            logger.ZLogError(ex, $"Dispense failed: {ex.Message}");
        }
        finally
        {
            lock (stateLock)
            {
                status = isError ? CashDispenseStatus.Error : CashDispenseStatus.Idle;
                lastErrorCode = code;
                lastErrorCodeExtended = codeEx;

                if (isError)
                {
                    errorEvents.OnNext(new DeviceErrorEventArgs(code, codeEx, DeviceErrorLocus.Output, DeviceErrorResponse.Retry));
                }
                else
                {
                    outputCompleteEvents.OnNext(new DeviceOutputCompleteEventArgs(0));
                }
            }

            changed.OnNext(Unit.Default);
        }
    }
}
