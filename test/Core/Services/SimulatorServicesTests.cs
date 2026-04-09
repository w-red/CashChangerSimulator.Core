using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using Microsoft.Extensions.Logging.Abstractions;
using CashChangerSimulator.Device.PosForDotNet;
using CashChangerSimulator.Device.Virtual;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Core.Services;

/// <summary>SimulatorServices の DI 抽象レイヤーを検証するテストクラス (TDD)。</summary>
public class SimulatorServicesTests : IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="SimulatorServicesTests"/> class.SimulatorServicesTests の新しいインスタンスを初期化します。</summary>
    public SimulatorServicesTests()
    {
        // 各テスト開始時にプロバイダーをクリア
        SimulatorServices.Provider = null;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        SimulatorServices.Provider = null;
        GC.SuppressFinalize(this);
    }

    /// <summary>プロバイダーが設定されていない場合に TryResolve が null を返すことを検証します。</summary>
    [Fact]
    public void TryResolveShouldReturnNullWhenProviderNotSet()
    {
        SimulatorServices.Provider = null;
        SimulatorServices.TryResolve<Inventory>().ShouldBeNull();
    }

    /// <summary>プロバイダーが設定されている場合に TryResolve がインスタンスを返すことを検証します。</summary>
    [Fact]
    public void TryResolveShouldReturnInstanceWhenProviderIsSet()
    {
        var inventory = Inventory.Create();
        var provider = new TestServiceProvider(inventory: inventory);
        SimulatorServices.Provider = provider;

        SimulatorServices.TryResolve<Inventory>().ShouldBeSameAs(inventory);
    }

    /// <summary>サービスプロバイダーが例外をスローした場合に、TryResolve が null を返すことを検証します。</summary>
    [Fact]
    public void TryResolveProviderThrowsShouldReturnNull()
    {
        var mock = new Mock<ISimulatorServiceProvider>();
        mock.Setup(m => m.Resolve<Inventory>()).Throws<InvalidOperationException>();
        SimulatorServices.Provider = mock.Object;

        SimulatorServices.TryResolve<Inventory>().ShouldBeNull();
    }

    /// <summary>サービスが登録されている場合に Resolve がインスタンスを返すことを検証します。</summary>
    [Fact]
    public void ResolveShouldReturnInstanceWhenProviderIsSet()
    {
        // Arrange
        var inventory = Inventory.Create();
        var provider = new TestServiceProvider(inventory: inventory);
        SimulatorServices.Provider = provider;

        // Act & Assert
        SimulatorServices.Resolve<Inventory>().ShouldBeSameAs(inventory);
    }

    /// <summary>プロバイダーが未設定の状態で Resolve を呼び出した際に InvalidOperationException が発生することを検証します。</summary>
    [Fact]
    public void ResolveNotRegisteredShouldThrow()
    {
        SimulatorServices.Provider = null;
        Should.Throw<InvalidOperationException>(() => SimulatorServices.Resolve<Inventory>());
    }

    /// <summary>InternalSimulatorCashChanger が利用可能な場合にプロバイダーのインスタンスを使用することを検証します。</summary>
    [Fact]
    public void SimulatorCashChangerShouldUseProviderInstancesWhenAvailable()
    {
        var configProvider = new ConfigurationProvider();
        var inventory = Inventory.Create();
        var history = new TransactionHistory();
        var hw = HardwareStatusManager.Create();
        var manager = new CashChangerManager(inventory, history, (object?)null, null);
        var metadataProvider = CurrencyMetadataProvider.Create(configProvider);
        var monitorsProvider = MonitorsProvider.Create(inventory, configProvider, metadataProvider);
        var aggregatorProvider = new OverallStatusAggregatorProvider(monitorsProvider);
        var depositController = new DepositController(inventory, hw);
        var timeProvider = new FakeTimeProvider();
        var dispenseController = new DispenseController(manager, inventory, configProvider, NullLoggerFactory.Instance, hw, new Mock<IDeviceSimulator>().Object, timeProvider);

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

        // InternalSimulatorCashChanger default constructor should pick up from SimulatorServices
        var cc = new InternalSimulatorCashChanger
        {
            SkipStateVerification = false
        };

        // Verify via ReadCashCounts (which uses Inventory internally)
        var ex = Should.Throw<Microsoft.PointOfService.PosControlException>(
            () => cc.ReadCashCounts());
        ex.ErrorCode.ShouldBe(Microsoft.PointOfService.ErrorCode.Closed);
    }

    /// <summary>テスト用の ISimulatorServiceProvider 実装。</summary>
    private class TestServiceProvider : ISimulatorServiceProvider
    {
        private readonly Dictionary<Type, object> services = [];

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
            if (configProvider != null)
            {
                services[typeof(ConfigurationProvider)] = configProvider;
            }

            if (inventory != null)
            {
                services[typeof(Inventory)] = inventory;
            }

            if (history != null)
            {
                services[typeof(TransactionHistory)] = history;
            }

            if (manager != null)
            {
                services[typeof(CashChangerManager)] = manager;
            }

            if (hw != null)
            {
                services[typeof(HardwareStatusManager)] = hw;
            }

            if (metadata != null)
            {
                services[typeof(CurrencyMetadataProvider)] = metadata;
            }

            if (monitors != null)
            {
                services[typeof(MonitorsProvider)] = monitors;
            }

            if (aggregator != null)
            {
                services[typeof(OverallStatusAggregatorProvider)] = aggregator;
            }

            if (deposit != null)
            {
                services[typeof(DepositController)] = deposit;
            }

            if (dispense != null)
            {
                services[typeof(DispenseController)] = dispense;
            }
        }

        public T Resolve<T>()
            where T : class
        {
            return services.TryGetValue(typeof(T), out var service)
                ? (T)service
                : throw new InvalidOperationException($"Service {typeof(T).Name} not registered.");
        }
    }
}
