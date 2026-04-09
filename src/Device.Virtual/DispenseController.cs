using System.Globalization;
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
public class DispenseController : IDisposable
{
    private readonly CashChangerManager manager;
    private readonly Inventory inventory;
    private readonly ConfigurationProvider configProvider;
    private readonly HardwareStatusManager hardwareStatusManager;
    private readonly IDeviceSimulator simulator;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<DispenseController> logger;
    private readonly CompositeDisposable disposables = [];
    private readonly Lock stateLock = new();
    private CancellationTokenSource? dispenseCts;
    private bool disposed;

    /// <summary>依存コンポーネントを指定してインスタンスを初期化します。</summary>
    /// <param name="manager">マネージャー。</param>
    /// <param name="inventory">在庫。</param>
    /// <param name="configProvider">設定プロバイダー。</param>
    /// <param name="loggerFactory">ロガーファクトリ。</param>
    /// <param name="hardwareStatusManager">ハードウェア状態管理。</param>
    /// <param name="simulator">デバイスシミュレーター。</param>
    /// <param name="timeProvider">時間プロバイダー。</param>
    public DispenseController(
        CashChangerManager manager,
        Inventory inventory,
        ConfigurationProvider configProvider,
        ILoggerFactory loggerFactory,
        HardwareStatusManager hardwareStatusManager,
        IDeviceSimulator simulator,
        TimeProvider? timeProvider = null)
    {
        this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
        this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        this.configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
        this.hardwareStatusManager = hardwareStatusManager ?? throw new ArgumentNullException(nameof(hardwareStatusManager));
        this.simulator = simulator ?? throw new ArgumentNullException(nameof(simulator));
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<DispenseController>();

        var changedSubject = new Subject<Unit>();
        disposables.Add(changedSubject);
        Changed = changedSubject;

        var outputCompleteEventsSubject = new Subject<DeviceOutputCompleteEventArgs>();
        disposables.Add(outputCompleteEventsSubject);
        OutputCompleteEvents = outputCompleteEventsSubject;

        var errorEventsSubject = new Subject<DeviceErrorEventArgs>();
        disposables.Add(errorEventsSubject);
        ErrorEvents = errorEventsSubject;

        Status = CashDispenseStatus.Idle;
        LastErrorCode = DeviceErrorCode.Success;
    }

    /// <summary>状態が変更されたときに通知されるストリーム。</summary>
    public Observable<Unit> Changed { get; }

    /// <summary>出力完了イベントを受け取るためのストリーム。</summary>
    public Observable<DeviceOutputCompleteEventArgs> OutputCompleteEvents { get; }

    /// <summary>エラーイベントを受け取るためのストリーム。</summary>
    public Observable<DeviceErrorEventArgs> ErrorEvents { get; }

    /// <summary>現在のステータスを取得します。</summary>
    public CashDispenseStatus Status { get; private set; }

    /// <summary>最後に発生したエラーコードを取得します。</summary>
    public DeviceErrorCode LastErrorCode { get; private set; }

    /// <summary>最後に発生した詳細エラーコードを取得します。</summary>
    public int LastErrorCodeExtended { get; private set; }

    /// <summary>処理中かどうかを取得します。</summary>
    public bool IsBusy => Status == CashDispenseStatus.Busy;

    /// <summary>シミュレーターを取得します（テスト用）。</summary>
    public IDeviceSimulator Simulator => simulator;

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
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
            if (!hardwareStatusManager.IsConnected.Value)
            {
                throw new DeviceException("Device is not connected.", DeviceErrorCode.Closed);
            }
        }

        var counts = ChangeCalculator.Calculate(inventory, amount, null, filter: k =>
        {
            var setting = configProvider.Config.GetDenominationSetting(k);
            return setting.IsRecyclable;
        });

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

            if (!hardwareStatusManager.IsConnected.Value)
            {
                throw new DeviceException("Device is not connected.", DeviceErrorCode.Closed);
            }

            if (hardwareStatusManager.IsJammed.Value)
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
            if (logger != null)
            {
                logger.ZLogError(ex, $"Dispense failed.");
            }

            HandleDispenseError(ex, out var code, out var codeEx);

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
            if (disposed)
            {
                return;
            }

            disposed = true;
        }

        if (disposing)
        {
            dispenseCts?.Cancel();
            dispenseCts?.Dispose();
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
    }
}
