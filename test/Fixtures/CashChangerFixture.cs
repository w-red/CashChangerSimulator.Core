using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Transactions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace CashChangerSimulator.Tests.Fixtures;

/// <summary>
/// キャッシュチェンジャーのテストに必要な一連のコンポーネントを管理するフィクスチャ。
/// </summary>
public class CashChangerFixture : IDisposable
{
    private bool _disposed;
    private CashChangerManager? _manager;

    public CashChangerFixture()
    {
        Inventory = Inventory.Create();
        ConfigurationProvider = new ConfigurationProvider();
        History = new TransactionHistory(ConfigurationProvider.Config);
        StatusManager = HardwareStatusManager.Create();
        TimeProvider = new FakeTimeProvider();
        LoggerFactory = NullLoggerFactory.Instance;
        ManagerMock = new Mock<CashChangerManager>(Inventory, History, ConfigurationProvider) { CallBase = true };
    }

    /// <summary>インベントリインスタンス</summary>
    public Inventory Inventory { get; set; }

    /// <summary>トランザクション履歴</summary>
    public TransactionHistory History { get; set; }

    /// <summary>設定プロバイダー</summary>
    public ConfigurationProvider ConfigurationProvider { get; set; }

    /// <summary>ハードウェア状態マネージャー</summary>
    public HardwareStatusManager StatusManager { get; set; }

    /// <summary>フェイクタイムプロバイダー</summary>
    public FakeTimeProvider TimeProvider { get; set; }

    /// <summary>ロガーファクトリー</summary>
    public ILoggerFactory LoggerFactory { get; set; }

    /// <summary>CashChangerManager のモック</summary>
    public Mock<CashChangerManager> ManagerMock { get; set; }

    /// <summary>CashChangerManager の実体。明示的に設定されていない場合は Mock オブジェクトを返します。</summary>
    public CashChangerManager Manager
    {
        get => _manager ?? ManagerMock.Object;
        set => _manager = value;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                Inventory?.Dispose();
                ConfigurationProvider?.Dispose();
                StatusManager?.Dispose();
            }
            _disposed = true;
        }
    }
}
