using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Core.Services;
using PosSharp.Abstractions;
using CashChangerSimulator.Core.Services.DeviceEventTypes;
using Microsoft.Extensions.Logging;
using R3;
using ZLogger;

namespace CashChangerSimulator.Device.Virtual;

/// <summary>仮想ハードウェアのシミュレーションロジックを統合し、ICashChangerDevice インターフェースを提供するクラス。</summary>
public sealed class VirtualCashChangerDevice : ICashChangerDevice
{
    private readonly DepositController depositController;
    private readonly DispenseController dispenseController;
    private readonly DiagnosticController diagnosticController;
    private readonly HardwareStatusManager hardwareStatus;
    private readonly CashChangerManager manager;
    private readonly Inventory inventory;
    private readonly ILogger<VirtualCashChangerDevice> logger;
    private readonly Mutex deviceMutex;
    private readonly CompositeDisposable disposables = [];
    private readonly Lock stateLock = new();
    private readonly string mutexName;

    private readonly ReactiveProperty<ControlState> state = new(ControlState.Closed);
    private readonly ReactiveProperty<bool> isBusy = new(false);
    private readonly ReadOnlyReactiveProperty<ControlState> stateReadOnly;
    private readonly ReadOnlyReactiveProperty<bool> isBusyReadOnly;

    private bool hasMutex;
    private bool disposed;

    /// <summary>依存コンポーネントを指定してインスタンスを初期化します。</summary>
    /// <param name="depositController">入金コントローラー。</param>
    /// <param name="dispenseController">出金コントローラー。</param>
    /// <param name="diagnosticController">診断コントローラー。</param>
    /// <param name="hardwareStatus">ハードウェア状態管理。</param>
    /// <param name="manager">釣銭機マネージャー。</param>
    /// <param name="inventory">現金在庫。</param>
    /// <param name="logger">ロガー。</param>
    /// <param name="mutexName">共有ミューテックス名。</param>
    internal VirtualCashChangerDevice(
        DepositController depositController,
        DispenseController dispenseController,
        DiagnosticController diagnosticController,
        HardwareStatusManager hardwareStatus,
        CashChangerManager manager,
        Inventory inventory,
        ILogger<VirtualCashChangerDevice> logger,
        string mutexName)
    {
        this.depositController = depositController ?? throw new ArgumentNullException(nameof(depositController));
        this.dispenseController = dispenseController ?? throw new ArgumentNullException(nameof(dispenseController));
        this.diagnosticController = diagnosticController ?? throw new ArgumentNullException(nameof(diagnosticController));
        this.hardwareStatus = hardwareStatus ?? throw new ArgumentNullException(nameof(hardwareStatus));
        this.manager = manager ?? throw new ArgumentNullException(nameof(manager));
        this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.mutexName = mutexName;
        deviceMutex = new Mutex(false, mutexName);
        isBusyReadOnly = isBusy.ToReadOnlyReactiveProperty().AddTo(disposables);
        stateReadOnly = state.ToReadOnlyReactiveProperty().AddTo(disposables);

        // 状態の統合監視
        Observable.CombineLatest(
            this.depositController.Changed,
            this.dispenseController.Changed,
            (a, b) => Unit.Default)
            .Subscribe(_ => UpdateCompositeStatus())
            .AddTo(disposables);

        this.hardwareStatus.IsConnected
            .Subscribe(_ => UpdateCompositeStatus())
            .AddTo(disposables);
    }

    /// <summary>入金コントローラーを取得します。</summary>
    public DepositController DepositController => depositController;

    /// <summary>出金コントローラーを取得します。</summary>
    public DispenseController DispenseController => dispenseController;

    /// <summary>診断コントローラーを取得します。</summary>
    public DiagnosticController DiagnosticController => diagnosticController;

    /// <summary>ハードウェア状態管理を取得します。</summary>
    public HardwareStatusManager HardwareStatus => hardwareStatus;

    /// <inheritdoc/>
    public ReadOnlyReactiveProperty<bool> IsBusy => isBusyReadOnly;

    /// <inheritdoc/>
    public ReadOnlyReactiveProperty<ControlState> State => stateReadOnly;

    /// <inheritdoc/>
    public Observable<UposDataEventArgs> DataEvents => depositController.DataEvents;

    /// <inheritdoc/>
    public Observable<UposErrorEventArgs> ErrorEvents =>
        Observable.Merge(depositController.ErrorEvents, dispenseController.ErrorEvents);

    /// <inheritdoc/>
    public Observable<UposStatusUpdateEventArgs> StatusUpdateEvents => hardwareStatus.StatusUpdateEvents;

    /// <inheritdoc/>
    public Observable<DeviceDirectIOEventArgs> DirectIOEvents => Observable.Empty<DeviceDirectIOEventArgs>();

    /// <inheritdoc/>
    public Observable<UposOutputCompleteEventArgs> OutputCompleteEvents => dispenseController.OutputCompleteEvents;

    /// <inheritdoc/>
    public Task OpenAsync()
    {
        lock (stateLock)
        {
            hardwareStatus.Input.IsConnected.Value = true;
            logger?.ZLogInformation($"VirtualCashChangerDevice Opened.");
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task CloseAsync()
    {
        lock (stateLock)
        {
            hardwareStatus.Input.IsConnected.Value = false;
            hardwareStatus.Input.DeviceEnabled.Value = false;
        }

        if (hasMutex)
        {
            ReleaseInternal();
        }

        UpdateCompositeStatus();
        logger?.ZLogInformation($"VirtualCashChangerDevice Closed.");

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ClaimAsync(int timeout)
    {
        if (!hardwareStatus.IsConnected.CurrentValue)
        {
            throw new DeviceException("Device not opened.", DeviceErrorCode.Closed);
        }

        try
        {
            hasMutex = deviceMutex.WaitOne(timeout);
            if (!hasMutex)
            {
                throw new DeviceException($"The device is already claimed. Mutex: {mutexName}", DeviceErrorCode.Claimed);
            }

            logger?.ZLogInformation($"VirtualCashChangerDevice Claimed. Mutex: {mutexName}");
        }
        catch (AbandonedMutexException)
        {
            hasMutex = true;
            logger?.ZLogWarning($"VirtualCashChangerDevice Claimed (AbandonedMutex rescued). Mutex: {mutexName}");
        }

        UpdateCompositeStatus();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ReleaseAsync()
    {
        ReleaseInternal();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task EnableAsync()
    {
        if (!hasMutex)
        {
            throw new DeviceException("Device not claimed.", DeviceErrorCode.Illegal);
        }

        hardwareStatus.Input.DeviceEnabled.Value = true;
        logger?.ZLogInformation($"VirtualCashChangerDevice Enabled.");

        UpdateCompositeStatus();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DisableAsync()
    {
        hardwareStatus.Input.DeviceEnabled.Value = false;
        logger?.ZLogInformation($"VirtualCashChangerDevice Disabled.");

        UpdateCompositeStatus();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task BeginDepositAsync()
    {
        EnsureEnabled();
        depositController.BeginDeposit();
        UpdateCompositeStatus();
        await Task.Yield();
    }

    /// <inheritdoc/>
    public async Task FixDepositAsync()
    {
        EnsureEnabled();
        depositController.FixDeposit();
        UpdateCompositeStatus();
        await Task.Yield();
    }

    /// <inheritdoc/>
    public async Task PauseDepositAsync(DeviceDepositPause control)
    {
        EnsureEnabled();
        depositController.PauseDeposit(control);
        UpdateCompositeStatus();
        await Task.Yield();
    }

    /// <inheritdoc/>
    public Task RepayDepositAsync()
    {
        EnsureEnabled();
        return depositController.RepayDepositAsync();
    }

    /// <inheritdoc/>
    public Task EndDepositAsync(DepositAction action)
    {
        EnsureEnabled();
        return depositController.EndDepositAsync(action);
    }

    /// <inheritdoc/>
    public Task DispenseChangeAsync(int amount)
    {
        EnsureEnabled();
        return dispenseController.DispenseChangeAsync(amount, false);
    }

    /// <inheritdoc/>
    public Task DispenseCashAsync(IEnumerable<CashDenominationCount> counts)
    {
        EnsureEnabled();
        var dict = counts.ToDictionary(
            c => FindKey(c.Denomination),
            c => c.Count);
        return dispenseController.DispenseCashAsync(dict, false);
    }

    /// <inheritdoc/>
    public Task<Inventory> ReadInventoryAsync()
    {
        return Task.FromResult(inventory);
    }

    /// <inheritdoc/>
    public async Task AdjustInventoryAsync(IEnumerable<CashDenominationCount> counts)
    {
        EnsureEnabled();
        manager.Adjust(counts.ToDictionary(
            c => FindKey(c.Denomination),
            c => c.Count));
        UpdateCompositeStatus();
        await Task.Yield();
    }

    /// <inheritdoc/>
    public async Task PurgeCashAsync()
    {
        EnsureEnabled();
        var counts = manager.PurgeCash();

        // 回収口の状態を更新
        hardwareStatus.Input.AddExitPortCounts(ExitPort.Collection, counts);

        UpdateCompositeStatus();
        await Task.Yield();
    }

    /// <inheritdoc/>
    public Task<string> CheckHealthAsync(HealthCheckLevel level)
    {
        return Task.FromResult(diagnosticController.GetHealthReport(level));
    }

    /// <inheritdoc/>
    public Task<int> DirectIOAsync(int command, int data, object obj)
    {
        EnsureEnabled();

        switch (command)
        {
            // TakeCash
            case DirectIOCommands.TakeCash:
                var port = (ExitPort)data;
                hardwareStatus.Input.ClearExitPort(port);
                return Task.FromResult(0);

            // GetExitPortCounts
            case DirectIOCommands.GetExitPortCounts:
                var targetPort = (ExitPort)data;
                var counts = hardwareStatus.State.GetExitPortCounts(targetPort);
                if (obj is IDictionary<DenominationKey, int> outDict)
                {
                    outDict.Clear();
                    foreach (var kv in counts)
                    {
                        outDict.Add(kv.Key, kv.Value);
                    }
                }
                return Task.FromResult(0);

            default:
                throw new DeviceException($"Unsupported DirectIO command: {command}", DeviceErrorCode.Illegal);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        // 1. 各コントローラーの破棄(内部でのキャンセルを誘発)
        depositController.Dispose();
        dispenseController.Dispose();
        diagnosticController.Dispose();

        // 2. ミューテックスの解放と破棄
        ReleaseInternal();
        deviceMutex.Dispose();

        // 3. リアクティブプロパティと購読の破棄
        disposables.Dispose();
        stateReadOnly.Dispose();
        isBusyReadOnly.Dispose();
        state.Dispose();
        isBusy.Dispose();

        disposed = true;
    }

    private void ReleaseInternal()
    {
        if (hasMutex)
        {
            try
            {
                deviceMutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Ignore release errors during shutdown if the mutex is already gone or not owned.
            }

            hasMutex = false;
            logger?.ZLogInformation($"VirtualCashChangerDevice Released. Mutex: {mutexName}");
        }
    }

    private void EnsureEnabled()
    {
        if (!hardwareStatus.IsConnected.CurrentValue)
        {
            throw new DeviceException("Device not opened.", DeviceErrorCode.Closed);
        }

        if (!hasMutex)
        {
            throw new DeviceException("Device not claimed.", DeviceErrorCode.Illegal);
        }

        if (!hardwareStatus.DeviceEnabled.CurrentValue)
        {
            throw new DeviceException("Device not enabled.", DeviceErrorCode.Disabled);
        }
    }

    private void UpdateCompositeStatus()
    {
        lock (stateLock)
        {
            bool busy = depositController.IsBusy || dispenseController.IsBusy;
            isBusy.Value = busy;

            if (!hardwareStatus.IsConnected.CurrentValue)
            {
                state.Value = ControlState.Closed;
            }
            else if (busy)
            {
                state.Value = ControlState.Busy;
            }
            else if (depositController.LastErrorCode != DeviceErrorCode.Success
                     || dispenseController.LastErrorCode != DeviceErrorCode.Success)
            {
                // エラー状態の判定(リカバリ待ち等の詳細ロジックは必要に応じて拡張)
                state.Value = ControlState.Error;
            }
            else
            {
                state.Value = ControlState.Idle;
            }
        }
    }

    private DenominationKey FindKey(decimal value)
    {
        var key = inventory.AllCounts.Select(kv => kv.Key).FirstOrDefault(k => k.Value == value)
                ?? throw new DeviceException($"Denomination {value} not found in inventory configuration.", DeviceErrorCode.Illegal);
        return key;
    }
}
