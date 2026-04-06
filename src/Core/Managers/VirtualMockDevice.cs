using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Device;
using Microsoft.Extensions.Logging;
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
    private bool hasMutex;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="VirtualMockDevice"/> class.仮想デバイスを初期化する。</summary>
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
    }

    /// <summary>Gets a value indicating whether デバイスがオープンされているかどうかを取得します。</summary>
    public bool IsConnected => statusManager.IsConnected.Value;

    /// <summary>Gets a value indicating whether デバイスが排他権（Claim）を取得しているかどうかを取得します。</summary>
    public bool Claimed { get; private set; }

    /// <summary>Gets a value indicating whether デバイスが有効化（Enable）されているかどうかを取得します。</summary>
    public bool DeviceEnabled { get; private set; }

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
            logger.ZLogInformation($"VirtualMockDevice Disposed.");
        }

        disposed = true;
    }
}
