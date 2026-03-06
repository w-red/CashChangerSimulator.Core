using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PointOfService;
using Moq;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Tests.Device;

public class UposConfigurationManagerTests
{
    private readonly ConfigurationProvider _configProvider;
    private readonly Inventory _inventory;
    private readonly Mock<IDeviceStateProvider> _stateProviderMock;
    private readonly UposConfigurationManager _manager;

    public UposConfigurationManagerTests()
    {
        _configProvider = new ConfigurationProvider();
        _inventory = new Inventory();
        _stateProviderMock = new Mock<IDeviceStateProvider>();
        _stateProviderMock.Setup(s => s.State).Returns(ControlState.Closed);
        _manager = new UposConfigurationManager(_configProvider, _inventory, _stateProviderMock.Object, NullLogger<UposConfigurationManager>.Instance);
    }

    [Fact]
    public void CurrencyCodeListShouldReflectConfiguration()
    {
        _configProvider.Config.Inventory["USD"] = new InventorySettings();
        _manager.CurrencyCodeList.ShouldContain("USD");
        _manager.CurrencyCodeList.ShouldContain("JPY");
    }

    [Fact]
    public void SettingCurrencyCodeShouldUpdateActiveCurrency()
    {
        _manager.Initialize();
        _manager.CurrencyCode = "JPY";
        _manager.CurrencyCode.ShouldBe("JPY");
    }

    [Fact]
    public void SettingUnsupportedCurrencyCodeShouldThrow()
    {
        Should.Throw<Exception>(() => _manager.CurrencyCode = "EUR");
    }

    [Fact]
    public void CurrencyCashListShouldReturnDenominations()
    {
        _configProvider.Config.Inventory["JPY"].Denominations["C100"] = new DenominationSettings { InitialCount = 10 };
        _inventory.SetCount(new DenominationKey(100, CurrencyCashType.Coin, "JPY"), 10);
        _manager.Initialize();
        _manager.CurrencyCode = "JPY";
        _manager.CurrencyCashList.Coins.ShouldContain(100);
    }
}
