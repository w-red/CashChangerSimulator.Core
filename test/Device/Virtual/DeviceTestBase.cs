using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Device.Virtual;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace CashChangerSimulator.Tests.Device.Virtual;

/// <summary>仮想デバイスコントローラーのテストセットアップを共通化するための基底クラス。</summary>
public abstract class DeviceTestBase : IDisposable
{
    private bool _disposed;

    protected DeviceTestBase()
    {
        Inventory = Inventory.Create();
        ConfigurationProvider = new ConfigurationProvider();
        // 仮想時間制御をテストするため、非ゼロの値を設定
        ConfigurationProvider.Config.Simulation.DispenseDelayMs = 1000;
        ConfigurationProvider.Config.Simulation.DepositDelayMs = 1000;
        History = new TransactionHistory(ConfigurationProvider.Config);
        StatusManager = HardwareStatusManager.Create();
        Manager = new CashChangerManager(Inventory, History, ConfigurationProvider);
        TimeProvider = new FakeTimeProvider();
        LoggerFactoryMock = new Mock<ILoggerFactory>();
        LoggerFactory = Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;
        DeviceFactory = new VirtualCashChangerDeviceFactory(ConfigurationProvider, LoggerFactory, TimeProvider);
    }

    /// <summary>インベントリインスタンス</summary>
    protected Inventory Inventory { get; }

    /// <summary>トランザクション履歴</summary>
    protected TransactionHistory History { get; }

    /// <summary>キャッシュチェンジャーマネージャー</summary>
    protected CashChangerManager Manager { get; }

    /// <summary>設定プロバイダー</summary>
    protected ConfigurationProvider ConfigurationProvider { get; }

    /// <summary>フェイクタイムプロバイダー</summary>
    public FakeTimeProvider TimeProvider { get; }

    /// <summary>ハードウェア状態マネージャー</summary>
    protected HardwareStatusManager StatusManager { get; }

    /// <summary>ロガーファクトリーの Mock</summary>
    protected Mock<ILoggerFactory> LoggerFactoryMock { get; }

    /// <summary>ロガーファクトリー</summary>
    protected ILoggerFactory LoggerFactory { get; }

    /// <summary>仮想デバイスファクトリー</summary>
    protected VirtualCashChangerDeviceFactory DeviceFactory { get; }

    /// <summary>各テストで固有の Mutex 名（Global\\TestMutex_{Guid}）を生成します。</summary>
    protected string GenerateUniqueMutexName() => $"Global\\TestMutex_{Guid.NewGuid()}";

    /// <summary>仮想デバイスインスタンスを生成します。</summary>
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

    /// <summary>指定された条件が満たされるまで待機します（FakeTimeProvider 対応）。</summary>
    protected async Task WaitUntil(Func<bool> condition, int timeoutSeconds = 5)
    {
        var startTimestamp = TimeProvider.GetTimestamp();
        var timeoutTicks = TimeSpan.FromSeconds(timeoutSeconds).Ticks;

        while (!condition())
        {
            var elapsedTicks = TimeProvider.GetTimestamp() - startTimestamp;
            if (elapsedTicks > timeoutTicks)
            {
                // Last check before failing
                if (condition()) return;
                
                throw new Xunit.Sdk.XunitException($"Condition was not met within {timeoutSeconds}s (virtual time)");
            }

            // [STABILITY] Automatically advance fake time to trigger background delays
            TimeProvider.Advance(TimeSpan.FromMilliseconds(10));

            // [STABILITY] Yield to allow background tasks to schedule and process state changes
            await Task.Delay(1).ConfigureAwait(false);
        }
    }
}
