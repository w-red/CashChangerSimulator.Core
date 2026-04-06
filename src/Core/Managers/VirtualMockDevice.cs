using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Services.DeviceEventTypes;
using Microsoft.Extensions.Logging;
using R3;
using ZLogger;

namespace CashChangerSimulator.Core.Managers;

/// <summary>
/// POS SDK に依存せず、純粋なロジックとして釣銭機の動作をエミュレートする仮想デバイス実装。
/// </summary>
public class VirtualMockDevice : ICashChangerDevice
{
    private const string MutexName = @"Global\CashChangerSimulatorDeviceMutex";

    private readonly CashChangerManager manager;
    private readonly Inventory inventory;
    private readonly HardwareStatusManager statusManager;
    private readonly ILogger<VirtualMockDevice> logger;
    private readonly Mutex deviceMutex = new(false, MutexName);
    private readonly Subject<DeviceDataEventArgs> dataEvents = new();
    private readonly Subject<DeviceErrorEventArgs> errorEvents = new();
    private readonly Subject<DeviceStatusUpdateEventArgs> statusUpdateEvents = new();
    private readonly Subject<DeviceDirectIOEventArgs> directIOEvents = new();
    private readonly Subject<DeviceOutputCompleteEventArgs> outputCompleteEvents = new();
    private readonly CompositeDisposable disposables = new();

    private bool hasMutex;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualMockDevice"/> class.
    /// 仮想デバイスを初期化する。
    /// </summary>
    /// <param name="manager">釣銭機マネージャー。</param>
    /// <param name="inventory">在庫。</param>
    /// <param name="statusManager">ハードウェアステータスマネージャー。</param>
    /// <param name="logger">ロガー。</param>
    public VirtualMockDevice(
        CashChangerManager manager,
        Inventory inventory,
        HardwareStatusManager statusManager,
        ILogger<VirtualMockDevice> logger)
    {
        this.manager = manager;
        this.inventory = inventory;
        this.statusManager = statusManager;
        this.logger = logger;

        // R3 Disposable pattern (example)
        this.statusManager.IsConnected
            .Subscribe(v => this.logger.ZLogDebug($"Connection status changed: {v}"))
            .AddTo(this.disposables);
    }

    /// <summary>デバイスがオープンされているかどうかを取得します。</summary>
    public bool IsConnected => statusManager.IsConnected.Value;

    /// <summary>デバイスが排他権（Claim）を取得しているかどうかを取得します。</summary>
    public bool Claimed { get; private set; }

    /// <summary>デバイスが有効化（Enable）されているかどうかを取得します。</summary>
    public bool DeviceEnabled { get; private set; }

    /// <inheritdoc/>
    public Observable<DeviceDataEventArgs> DataEvents => dataEvents;

    /// <inheritdoc/>
    public Observable<DeviceErrorEventArgs> ErrorEvents => errorEvents;

    /// <inheritdoc/>
    public Observable<DeviceStatusUpdateEventArgs> StatusUpdateEvents => statusUpdateEvents;

    /// <inheritdoc/>
    public Observable<DeviceDirectIOEventArgs> DirectIOEvents => directIOEvents;

    /// <inheritdoc/>
    public Observable<DeviceOutputCompleteEventArgs> OutputCompleteEvents => outputCompleteEvents;

    /// <inheritdoc/>
    public ReadOnlyReactiveProperty<bool> IsBusy => Observable.Return(false).ToReadOnlyReactiveProperty(false);

    /// <inheritdoc/>
    public ReadOnlyReactiveProperty<DeviceControlState> State => Observable.Return(DeviceControlState.Idle).ToReadOnlyReactiveProperty(DeviceControlState.Idle);

    /// <summary>デバイスをプログラム的にオープンします。</summary>
    public void Open()
    {
        statusManager.SetConnected(true);
        logger.ZLogInformation($"VirtualMockDevice Opened.");
    }

    /// <summary>デバイスをクローズし、リソースを解放します。</summary>
    public void Close()
    {
        Disable();
        Release();
        statusManager.SetConnected(false);
        logger.ZLogInformation($"VirtualMockDevice Closed.");
    }

    /// <summary>デバイスの排他権を取得します。</summary>
    /// <param name="timeout">タイムアウト（ミリ秒）。</param>
    public void Claim(int timeout)
    {
        if (!IsConnected)
        {
            throw new InvalidOperationException("Device not opened.");
        }

        try
        {
            // Mutex によるプロセス間/インスタンス間排他
            hasMutex = deviceMutex.WaitOne(timeout);
            if (!hasMutex)
            {
                throw new DeviceException("The device is already claimed by another process or instance.", DeviceErrorCode.Claimed);
            }

            Claimed = true;
            logger.ZLogInformation($"VirtualMockDevice Claimed.");
        }
        catch (AbandonedMutexException)
        {
            // 前のプロセスがクラッシュなどで Mutex を解放せずに終了した場合
            hasMutex = true;
            Claimed = true;
            logger.ZLogWarning($"VirtualMockDevice Claimed (AbandonedMutex rescued).");
        }
    }

    /// <summary>デバイスの排他権を解放します。</summary>
    public void Release()
    {
        if (hasMutex)
        {
            deviceMutex.ReleaseMutex();
            hasMutex = false;
        }

        Claimed = false;
        logger.ZLogInformation($"VirtualMockDevice Released.");
    }

    /// <inheritdoc/>
    public Task OpenAsync()
    {
        Open();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task CloseAsync()
    {
        Close();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ClaimAsync(int timeout)
    {
        Claim(timeout);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ReleaseAsync()
    {
        Release();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task EnableAsync()
    {
        Enable();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DisableAsync()
    {
        Disable();
        return Task.CompletedTask;
    }

    /// <summary>デバイスを有効化し、入排金操作を可能にします。</summary>
    public void Enable()
    {
        if (!Claimed)
        {
            throw new InvalidOperationException("Device not claimed.");
        }

        DeviceEnabled = true;
        logger.ZLogInformation($"VirtualMockDevice Enabled.");
    }

    /// <summary>デバイスを無効化します。</summary>
    public void Disable()
    {
        DeviceEnabled = false;
        logger.ZLogInformation($"VirtualMockDevice Disabled.");
    }

    /// <summary>現金を投入します。</summary>
    /// <param name="counts">投入する金種と枚数。</param>
    public void Deposit(IReadOnlyDictionary<DenominationKey, int> counts)
    {
        if (!DeviceEnabled)
        {
            throw new InvalidOperationException("Device not enabled.");
        }

        manager.Deposit(counts);
    }

    /// <summary>現金を払い出します。</summary>
    /// <param name="amount">払い出す合計金額。</param>
    /// <param name="currencyCode">通貨コード（任意）。</param>
    public void Dispense(decimal amount, string? currencyCode = null)
    {
        if (!DeviceEnabled)
        {
            throw new InvalidOperationException("Device not enabled.");
        }

        manager.Dispense(amount, currencyCode);
    }

    /// <inheritdoc/>
    public Task BeginDepositAsync() => Task.FromException(new NotImplementedException());

    /// <inheritdoc/>
    public Task EndDepositAsync(DepositAction action) => Task.FromException(new NotImplementedException());

    /// <inheritdoc/>
    public Task FixDepositAsync() => Task.FromException(new NotImplementedException());

    /// <inheritdoc/>
    public Task PauseDepositAsync(DeviceDepositPause control) => Task.FromException(new NotImplementedException());

    /// <inheritdoc/>
    public Task RepayDepositAsync() => Task.FromException(new NotImplementedException());

    /// <inheritdoc/>
    public Task DispenseChangeAsync(int amount)
    {
        Dispense(amount);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DispenseCashAsync(IEnumerable<CashDenominationCount> counts)
    {
        manager.Dispense(counts.ToDictionary(c => FindKey(c.Denomination), c => c.Count));
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<Inventory> ReadInventoryAsync() => Task.FromResult(inventory);

    /// <inheritdoc/>
    public Task AdjustInventoryAsync(IEnumerable<CashDenominationCount> counts)
    {
        manager.Adjust(counts.ToDictionary(c => FindKey(c.Denomination), c => c.Count));
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task PurgeCashAsync()
    {
        manager.PurgeCash();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<string> CheckHealthAsync(DeviceHealthCheckLevel level) => Task.FromResult("OK");

    /// <summary>現在の在庫情報を取得します。</summary>
    /// <returns>在庫情報のコピー。</returns>
    public IReadOnlyDictionary<DenominationKey, int> GetInventory()
    {
        return inventory.AllCounts.ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>リソースを解放します。</summary>
    /// <param name="disposing">明示的な破棄かどうか。</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        if (disposing)
        {
            Release();
            deviceMutex.Dispose();
            dataEvents.OnCompleted();
            dataEvents.Dispose();
            errorEvents.OnCompleted();
            errorEvents.Dispose();
            statusUpdateEvents.OnCompleted();
            statusUpdateEvents.Dispose();
            directIOEvents.OnCompleted();
            directIOEvents.Dispose();
            outputCompleteEvents.OnCompleted();
            outputCompleteEvents.Dispose();
            disposables.Dispose();
            logger.ZLogInformation($"VirtualMockDevice Disposed.");
        }

        disposed = true;
    }

    private DenominationKey FindKey(decimal value)
    {
        // 在庫情報から、対応する金種を検索する（なければ例外、またはデフォルト生成を検討）
        var key = inventory.AllCounts.Select(kv => kv.Key).FirstOrDefault(k => k.Value == value)
                ?? throw new DeviceException($"Denomination {value} not found in inventory configuration.", DeviceErrorCode.Illegal);
        return key;
    }
}
