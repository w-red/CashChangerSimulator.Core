using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using Microsoft.Extensions.Logging;

namespace CashChangerSimulator.Device.Virtual;

/// <summary>仮想釣銭機デバイスを生成するためのファクトリクラス。</summary>
public sealed class VirtualCashChangerDeviceFactory : ICashChangerDeviceFactory
{
    /// <summary>デフォルトの共有ミューテックス名。</summary>
    public const string DefaultMutexName = @"Global\CashChangerSimulatorVirtualDeviceMutex";

    private readonly ConfigurationProvider configurationProvider;
    private readonly TimeProvider timeProvider;
    private readonly ILoggerFactory loggerFactory;

    /// <summary>依存コンポーネントを指定してインスタンスを初期化します。</summary>
    /// <param name="configurationProvider">設定プロバイダー。</param>
    /// <param name="loggerFactory">ロガーファクトリ。</param>
    /// <param name="timeProvider">時間プロバイダー。</param>
    public VirtualCashChangerDeviceFactory(
        ConfigurationProvider configurationProvider,
        ILoggerFactory loggerFactory,
        TimeProvider? timeProvider = null)
    {
        this.configurationProvider = configurationProvider ?? throw new ArgumentNullException(nameof(configurationProvider));
        this.loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public ICashChangerDevice Create(CashChangerManager manager, Inventory inventory, HardwareStatusManager statusManager)
    {
        return Create(manager, inventory, statusManager, DefaultMutexName);
    }

    /// <summary>指定された Mutex 名を使用して仮想デバイスを生成します。</summary>
    /// <param name="manager">マネージャー。</param>
    /// <param name="inventory">在庫。</param>
    /// <param name="statusManager">ステータスマネージャー。</param>
    /// <param name="mutexName">ミューテックス名。</param>
    /// <returns>生成された仮想デバイス。</returns>
    public ICashChangerDevice Create(CashChangerManager manager, Inventory inventory, HardwareStatusManager statusManager, string mutexName)
    {
        var depositController = new DepositController(inventory, statusManager, manager, configurationProvider, timeProvider);
        var dispenseController = new DispenseController(manager, inventory, configurationProvider, loggerFactory, statusManager, HardwareSimulator.Create(configurationProvider, timeProvider));
        var diagnosticController = new DiagnosticController(inventory, statusManager);

        return new VirtualCashChangerDevice(
            depositController,
            dispenseController,
            diagnosticController,
            statusManager,
            manager,
            inventory,
            loggerFactory.CreateLogger<VirtualCashChangerDevice>(),
            mutexName);
    }
}
