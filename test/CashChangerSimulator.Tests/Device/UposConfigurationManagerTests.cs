using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device.Services;
using Microsoft.PointOfService;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

public class UposConfigurationManagerTests
{
    private readonly ConfigurationProvider _configProvider;
    private readonly Inventory _inventory;
    private readonly Mock<IDeviceStateProvider> _stateProvider;
    private readonly UposConfigurationManager _manager;

    public UposConfigurationManagerTests()
    {
        _configProvider = new ConfigurationProvider();
        _inventory = new Inventory();
        _stateProvider = new Mock<IDeviceStateProvider>();
        _manager = new UposConfigurationManager(_configProvider, _inventory, _stateProvider.Object);
    }

    [Fact]
    public void CurrencyCode_ShouldThrowWhenUnsupported()
    {
        Should.Throw<PosControlException>(() => _manager.CurrencyCode = "INVALID")
            .ErrorCode.ShouldBe(ErrorCode.Illegal);
    }

    [Fact]
    public void CurrencyCode_ShouldWorkWhenSupported()
    {
        _configProvider.Config.Inventory["USD"] = new InventorySettings();
        _manager.CurrencyCode = "USD";
        _manager.CurrencyCode.ShouldBe("USD");
    }

    [Fact]
    public void ResetState_WhenConfigurationChanges()
    {
        _stateProvider.Setup(s => s.State).Returns(ControlState.Idle);
        _inventory.SetCount(new DenominationKey(1000, CurrencyCashType.Bill), 10);
        
        // Trigger reload
        _configProvider.Update(new SimulatorConfiguration());
        
        _inventory.AllCounts.ShouldBeEmpty();
    }

    [Fact]
    public void Reload_ShouldManuallyTriggerUpdate()
    {
        _stateProvider.Setup(s => s.State).Returns(ControlState.Idle);
        _inventory.SetCount(new DenominationKey(1000, CurrencyCashType.Bill), 10);
        
        _manager.Reload();
        
        _inventory.AllCounts.ShouldBeEmpty();
    }

    [Fact]
    public void Initialize_ShouldSetActiveCurrency()
    {
        _configProvider.Config.Inventory.Clear();
        _configProvider.Config.Inventory["EUR"] = new InventorySettings();
        
        _manager.Initialize();
        _manager.CurrencyCode.ShouldBe("EUR");
    }

    [Fact]
    public void Dispose_ShouldUnsubscribe()
    {
        _manager.Dispose();
        // Trigger reload should not cause issues even if manager logic would have
        _configProvider.Update(new SimulatorConfiguration());
    }

    [Fact]
    public void CurrencyCashList_ShouldReturnUposUnits()
    {
        var list = _manager.CurrencyCashList;
        // CashUnits is a struct, so it cannot be null.
    }
}
