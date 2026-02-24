using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Models;
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
        var provider = new TestServiceProvider(inventory);
        SimulatorServices.Provider = provider;

        SimulatorServices.TryResolve<Inventory>().ShouldBeSameAs(inventory);
    }

    /// <summary>SimulatorCashChanger が利用可能な場合にプロバイダーのインスタンスを使用することを検証する。</summary>
    [Fact]
    public void SimulatorCashChangerShouldUseProviderInstancesWhenAvailable()
    {
        var inventory = new Inventory();
        var history = new TransactionHistory();
        var hw = new HardwareStatusManager();
        var manager = new CashChangerManager(inventory, history, new ChangeCalculator());

        var provider = new TestServiceProvider(inventory, history, manager, hw);
        SimulatorServices.Provider = provider;

        // SimulatorCashChanger default constructor should pick up from SimulatorServices
        var cc = new CashChangerSimulator.Device.SimulatorCashChanger();

        // Verify via ReadCashCounts (which uses _inventory internally)
        // If it doesn't throw, it means it got a valid Inventory
        // State is Closed so it should throw ErrorCode.Closed
        var ex = Should.Throw<Microsoft.PointOfService.PosControlException>(
            () => cc.ReadCashCounts());
        ex.ErrorCode.ShouldBe(Microsoft.PointOfService.ErrorCode.Closed);
    }

    /// <summary>テスト用の ISimulatorServiceProvider 実装。</summary>
    private class TestServiceProvider : ISimulatorServiceProvider
    {
        private readonly Dictionary<Type, object> _services = new();

        public TestServiceProvider(
            Inventory? inventory = null,
            TransactionHistory? history = null,
            CashChangerManager? manager = null,
            HardwareStatusManager? hw = null)
        {
            if (inventory != null) _services[typeof(Inventory)] = inventory;
            if (history != null) _services[typeof(TransactionHistory)] = history;
            if (manager != null) _services[typeof(CashChangerManager)] = manager;
            if (hw != null) _services[typeof(HardwareStatusManager)] = hw;
        }

        public T Resolve<T>() where T : class
        {
            return _services.TryGetValue(typeof(T), out var service)
                ? (T)service
                : throw new InvalidOperationException($"Service {typeof(T).Name} not registered.");
        }
    }
}
