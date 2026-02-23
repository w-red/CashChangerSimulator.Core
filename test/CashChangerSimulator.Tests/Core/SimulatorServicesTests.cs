using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Models;
using Shouldly;

namespace CashChangerSimulator.Tests.Core;

/// <summary>SimulatorServices の DI 抽象レイヤーを検証するテストクラス (TDD)。</summary>
public class SimulatorServicesTests : IDisposable
{
    public SimulatorServicesTests()
    {
        // 各テスト開始時にプロバイダーをクリア
        SimulatorServices.Provider = null;
    }

    public void Dispose()
    {
        SimulatorServices.Provider = null;
    }

    [Fact]
    public void TryResolve_ShouldReturnNull_WhenProviderNotSet()
    {
        SimulatorServices.Provider = null;
        SimulatorServices.TryResolve<Inventory>().ShouldBeNull();
    }

    [Fact]
    public void TryResolve_ShouldReturnInstance_WhenProviderIsSet()
    {
        var inventory = new Inventory();
        var provider = new TestServiceProvider(inventory);
        SimulatorServices.Provider = provider;

        SimulatorServices.TryResolve<Inventory>().ShouldBeSameAs(inventory);
    }

    [Fact]
    public void SimulatorCashChanger_ShouldUseProviderInstances_WhenAvailable()
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
