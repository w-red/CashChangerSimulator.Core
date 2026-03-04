using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Core;

/// <summary>SimulatorServices の DI 抽象レイヤーを検証するテストクラス (TDD)。</summary>
public class SimulatorServicesTests : IDisposable
{
    /// <summary>SimulatorServicesTests の新しいインスタンスを初期化します。</summary>
    public SimulatorServicesTests()
    {
        // 各テスト開始時にプロバイダーをクリア
        SimulatorServices.Provider = null;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        SimulatorServices.Provider = null;
    }

    /// <summary>プロバイダーが設定されていない場合に TryResolve が null を返すことを検証する。</summary>
    [Fact]
    public void TryResolveShouldReturnNullWhenProviderNotSet()
    {
        SimulatorServices.Provider = null;
        SimulatorServices.TryResolve<Inventory>().ShouldBeNull();
    }

    /// <summary>プロバイダーが設定されている場合に TryResolve がインスタンスを返すことを検証する。</summary>
    [Fact]
    public void TryResolveShouldReturnInstanceWhenProviderIsSet()
    {
        var inventory = new Inventory();
        var provider = new TestServiceProvider(inventory: inventory);
        SimulatorServices.Provider = provider;

        SimulatorServices.TryResolve<Inventory>().ShouldBeSameAs(inventory);
    }

    /// <summary>SimulatorCashChanger が利用可能な場合にプロバイダーのインスタンスを使用することを検証する。</summary>
    [Fact]
    public void SimulatorCashChangerShouldUseProviderInstancesWhenAvailable()
    {
        var configProvider = new ConfigurationProvider();
        var inventory = new Inventory();
        var history = new TransactionHistory();
        var hw = new HardwareStatusManager();
        var manager = new CashChangerManager(inventory, history, new ChangeCalculator());
        var metadataProvider = new CurrencyMetadataProvider(configProvider);
        var monitorsProvider = new MonitorsProvider(inventory, configProvider, metadataProvider);
        var aggregatorProvider = new OverallStatusAggregatorProvider(monitorsProvider);
        var depositController = new DepositController(inventory, hw);
        var dispenseController = new DispenseController(manager, hw, new Mock<IDeviceSimulator>().Object);

        var provider = new TestServiceProvider(
            configProvider, 
            inventory, 
            history, 
            manager, 
            hw, 
            metadataProvider, 
            monitorsProvider, 
            aggregatorProvider, 
            depositController, 
            dispenseController);
            
        SimulatorServices.Provider = provider;

        // SimulatorCashChanger default constructor should pick up from SimulatorServices
        var cc = new SimulatorCashChanger();

        // Verify via ReadCashCounts (which uses _inventory internally)
        var ex = Should.Throw<Microsoft.PointOfService.PosControlException>(
            () => cc.ReadCashCounts());
        ex.ErrorCode.ShouldBe(Microsoft.PointOfService.ErrorCode.Closed);
    }

    /// <summary>テスト用の ISimulatorServiceProvider 実装。</summary>
    private class TestServiceProvider : ISimulatorServiceProvider
    {
        private readonly Dictionary<Type, object> _services = [];

        public TestServiceProvider(
            ConfigurationProvider? configProvider = null,
            Inventory? inventory = null,
            TransactionHistory? history = null,
            CashChangerManager? manager = null,
            HardwareStatusManager? hw = null,
            CurrencyMetadataProvider? metadata = null,
            MonitorsProvider? monitors = null,
            OverallStatusAggregatorProvider? aggregator = null,
            DepositController? deposit = null,
            DispenseController? dispense = null)
        {
            if (configProvider != null) _services[typeof(ConfigurationProvider)] = configProvider;
            if (inventory != null) _services[typeof(Inventory)] = inventory;
            if (history != null) _services[typeof(TransactionHistory)] = history;
            if (manager != null) _services[typeof(CashChangerManager)] = manager;
            if (hw != null) _services[typeof(HardwareStatusManager)] = hw;
            if (metadata != null) _services[typeof(CurrencyMetadataProvider)] = metadata;
            if (monitors != null) _services[typeof(MonitorsProvider)] = monitors;
            if (aggregator != null) _services[typeof(OverallStatusAggregatorProvider)] = aggregator;
            if (deposit != null) _services[typeof(DepositController)] = deposit;
            if (dispense != null) _services[typeof(DispenseController)] = dispense;
        }

        public T Resolve<T>() where T : class
        {
            return _services.TryGetValue(typeof(T), out var service)
                ? (T)service
                : throw new InvalidOperationException($"Service {typeof(T).Name} not registered.");
        }
    }
}
