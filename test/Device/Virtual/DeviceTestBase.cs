using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Device.Virtual;
using Microsoft.Extensions.Logging;
using Moq;

namespace CashChangerSimulator.Tests.Device.Virtual;

/// <summary>
/// 仮想デバイスコントローラーのテストセットアップを共通化するための基底クラス。
/// </summary>
public abstract class DeviceTestBase : IDisposable
{
    private bool _disposed;

    protected DeviceTestBase()
    {
        Inventory = Inventory.Create();
        ConfigurationProvider = new ConfigurationProvider();
        History = new TransactionHistory(ConfigurationProvider.Config);
        StatusManager = HardwareStatusManager.Create();
        Manager = new CashChangerManager(Inventory, History, ConfigurationProvider);

        LoggerFactoryMock = new Mock<ILoggerFactory>();
        LoggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(new Mock<ILogger>().Object);

        DeviceFactory = new VirtualCashChangerDeviceFactory(ConfigurationProvider, LoggerFactoryMock.Object);
    }

    /// <summary>インベントリインスタンス</summary>
    protected Inventory Inventory { get; }

    /// <summary>トランザクション履歴</summary>
    protected TransactionHistory History { get; }

    /// <summary>キャッシュチェンジャーマネージャー</summary>
    protected CashChangerManager Manager { get; }

    /// <summary>設定プロバイダー</summary>
    protected ConfigurationProvider ConfigurationProvider { get; }

    /// <summary>ハードウェア状態マネージャー</summary>
    protected HardwareStatusManager StatusManager { get; }

    /// <summary>ロガーファクトリーの Mock</summary>
    protected Mock<ILoggerFactory> LoggerFactoryMock { get; }

    /// <summary>仮想デバイスファクトリー</summary>
    protected VirtualCashChangerDeviceFactory DeviceFactory { get; }

    /// <summary>
    /// 各テストで固有の Mutex 名（Global\\TestMutex_{Guid}）を生成します。
    /// </summary>
    protected string GenerateUniqueMutexName() => $"Global\\TestMutex_{Guid.NewGuid()}";

    /// <summary>
    /// 仮想デバイスインスタンスを生成します。
    /// </summary>
    protected ICashChangerDevice CreateDevice(string? mutexName = null)
    {
        return DeviceFactory.Create(Manager, Inventory, StatusManager, mutexName ?? GenerateUniqueMutexName());
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                Inventory.Dispose();
                ConfigurationProvider.Dispose();
                StatusManager.Dispose();
                // History, Manager は IDisposable ではない
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
