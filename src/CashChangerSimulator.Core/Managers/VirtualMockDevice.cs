using System.Threading;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace CashChangerSimulator.Core.Managers;

/// <summary>
/// POS SDK に依存せず、純粋なロジックとして釣銭機の動作をエミュレートする仮想デバイス実装。
/// </summary>
public class VirtualMockDevice : ICashChangerDevice
{
    private readonly CashChangerManager _manager;
    private readonly Inventory _inventory;
    private readonly HardwareStatusManager _statusManager;
    private readonly ILogger<VirtualMockDevice> _logger;

    public bool IsConnected => _statusManager.IsConnected.Value;
    public bool Claimed { get; private set; }
    public bool DeviceEnabled { get; private set; }

    private const string MutexName = @"Global\CashChangerSimulatorDeviceMutex";
    private readonly Mutex _deviceMutex = new(false, MutexName);
    private bool _hasMutex;

    public VirtualMockDevice(
        CashChangerManager manager,
        Inventory inventory,
        HardwareStatusManager statusManager,
        ILogger<VirtualMockDevice> logger)
    {
        _manager = manager;
        _inventory = inventory;
        _statusManager = statusManager;
        _logger = logger;
    }

    public void Open()
    {
        _statusManager.SetConnected(true);
        _logger.ZLogInformation($"VirtualMockDevice Opened.");
    }

    public void Close()
    {
        Disable();
        Release();
        _statusManager.SetConnected(false);
        _logger.ZLogInformation($"VirtualMockDevice Closed.");
    }

    public void Claim(int timeout)
    {
        if (!IsConnected) throw new InvalidOperationException("Device not opened.");
        
        try
        {
            // Mutex によるプロセス間/インスタンス間排他
            _hasMutex = _deviceMutex.WaitOne(timeout);
            if (!_hasMutex)
            {
                throw new Exception("The device is already claimed by another process or instance.");
            }
            
            Claimed = true;
            _logger.ZLogInformation($"VirtualMockDevice Claimed.");
        }
        catch (AbandonedMutexException)
        {
            // 前のプロセスがクラッシュなどで Mutex を解放せずに終了した場合
            _hasMutex = true;
            Claimed = true;
            _logger.ZLogWarning($"VirtualMockDevice Claimed (AbandonedMutex rescued).");
        }
    }

    public void Release()
    {
        if (_hasMutex)
        {
            _deviceMutex.ReleaseMutex();
            _hasMutex = false;
        }
        
        Claimed = false;
        _logger.ZLogInformation($"VirtualMockDevice Released.");
    }

    public void Enable()
    {
        if (!Claimed) throw new InvalidOperationException("Device not claimed.");
        DeviceEnabled = true;
        _logger.ZLogInformation($"VirtualMockDevice Enabled.");
    }

    public void Disable()
    {
        DeviceEnabled = false;
        _logger.ZLogInformation($"VirtualMockDevice Disabled.");
    }

    public void Deposit(IReadOnlyDictionary<DenominationKey, int> counts)
    {
        if (!DeviceEnabled) throw new InvalidOperationException("Device not enabled.");
        _manager.Deposit(counts);
    }

    public void Dispense(decimal amount, string? currencyCode = null)
    {
        if (!DeviceEnabled) throw new InvalidOperationException("Device not enabled.");
        _manager.Dispense(amount, currencyCode);
    }

    public IReadOnlyDictionary<DenominationKey, int> GetInventory()
    {
        return _inventory.AllCounts.ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    public void Dispose()
    {
        Release();
        _deviceMutex.Dispose();
        _logger.ZLogInformation($"VirtualMockDevice Disposed.");
        GC.SuppressFinalize(this);
    }
}
