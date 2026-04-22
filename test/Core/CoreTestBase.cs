using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Tests.Fixtures;
using Moq;

namespace CashChangerSimulator.Tests.Core;

/// <summary>Core プロジェクトのテストセットアップを共通化するための基底クラス</summary>
public abstract class CoreTestBase : IDisposable
{
    private bool _disposed;

    protected CoreTestBase()
    {
        Fixture = new CashChangerFixture();
        
        // 既存のオーバーライドを尊重し、フィクスチャの状態を同期させる
        Fixture.Inventory = CreateInventory();
        Fixture.ConfigurationProvider = CreateConfigurationProvider();
        Fixture.History = CreateHistory(Fixture.ConfigurationProvider);
        
        // マネージャーの同期
        var manager = CreateManager(Fixture.Inventory, Fixture.History, Fixture.ConfigurationProvider);
        
        // S1066 Fix: Use guard clauses to flatten the nesting
        if (manager is null) return;
        if (manager == Fixture.ManagerMock.Object) return;

        if (manager is IMocked mocked)
        {
            Fixture.ManagerMock = (Mock<CashChangerManager>)mocked.Mock;
            return;
        }

        Fixture.Manager = manager;
    }

    /// <summary>共通フィクスチャ</summary>
    protected CashChangerFixture Fixture { get; }

    /// <summary>インベントリインスタンス(実クラスまたはMockオブジェクト)</summary>
    protected Inventory Inventory => Fixture.Inventory;

    /// <summary>トランザクション履歴</summary>
    protected TransactionHistory History => Fixture.History;

    /// <summary>キャッシュチェンジャーマネージャー</summary>
    protected CashChangerManager Manager => Fixture.Manager;

    /// <summary>設定プロバイダー</summary>
    protected ConfigurationProvider ConfigurationProvider => Fixture.ConfigurationProvider;

    /// <summary>Inventory を生成します。オーバーライドして Moq や Logger を注入できます。</summary>
    protected virtual Inventory CreateInventory() => Inventory.Create();

    /// <summary>TransactionHistory を生成します。Moq に差し替える場合はオーバーライドします。</summary>
    protected virtual TransactionHistory CreateHistory(ConfigurationProvider configProvider) => new TransactionHistory(configProvider.Config);

    /// <summary>ConfigurationProvider を生成します。Moq に差し替える場合はオーバーライドします。</summary>
    protected virtual ConfigurationProvider CreateConfigurationProvider() => new ConfigurationProvider();

    /// <summary>CashChangerManager を生成します。Moq に差し替える場合はオーバーライドします。</summary>
    protected virtual CashChangerManager CreateManager(
        Inventory inventory,
        TransactionHistory history,
        ConfigurationProvider configProvider) => new CashChangerManager(inventory, history, configProvider);

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                Fixture.Dispose();
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
