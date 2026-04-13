using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Transactions;
using Moq;

namespace CashChangerSimulator.Tests.Core;

/// <summary>Core プロジェクトのテストセットアップを共通化するための基底クラス。</summary>
public abstract class CoreTestBase : IDisposable
{
    private bool _disposed;

    protected CoreTestBase()
    {
        Inventory = CreateInventory();
        ConfigurationProvider = CreateConfigurationProvider();
        History = CreateHistory(ConfigurationProvider);
        Manager = CreateManager(Inventory, History, ConfigurationProvider);
    }

    /// <summary>インベントリインスタンス(実クラスまたはMockオブジェクト)</summary>
    protected Inventory Inventory { get; }

    /// <summary>トランザクション履歴</summary>
    protected TransactionHistory History { get; }

    /// <summary>キャッシュチェンジャーマネージャー</summary>
    protected CashChangerManager Manager { get; }

    /// <summary>設定プロバイダー</summary>
    protected ConfigurationProvider ConfigurationProvider { get; }

    /// <summary>Inventory を生成します。オーバーライドして Moq や Logger を注入できます。</summary>
    protected virtual Inventory CreateInventory()
    {
        return CashChangerSimulator.Core.Models.Inventory.Create();
    }

    /// <summary>TransactionHistory を生成します。Moq に差し替える場合はオーバーライドします。</summary>
    protected virtual TransactionHistory CreateHistory(ConfigurationProvider configProvider)
    {
        return new TransactionHistory(configProvider.Config);
    }

    /// <summary>ConfigurationProvider を生成します。Moq に差し替える場合はオーバーライドします。</summary>
    protected virtual ConfigurationProvider CreateConfigurationProvider()
    {
        return new ConfigurationProvider();
    }

    /// <summary>CashChangerManager を生成します。Moq に差し替える場合はオーバーライドします。</summary>
    protected virtual CashChangerManager CreateManager(
        Inventory inventory, 
        TransactionHistory history, 
        ConfigurationProvider configProvider)
    {
        return new CashChangerManager(inventory, history, configProvider);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                if (Inventory is IDisposable d)
                {
                    d.Dispose();
                }

                if (ConfigurationProvider is IDisposable cd)
                {
                    cd.Dispose();
                }

                if (History is IDisposable hd)
                {
                    hd.Dispose();
                }
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
