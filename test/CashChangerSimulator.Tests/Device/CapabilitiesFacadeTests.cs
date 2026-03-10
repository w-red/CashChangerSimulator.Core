using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Device.Facades;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

public class CapabilitiesFacadeTests
{
    private readonly SimulatorConfiguration _config;
    private readonly CapabilitiesFacade _sut;

    public CapabilitiesFacadeTests()
    {
        _config = new SimulatorConfiguration();
        _sut = new CapabilitiesFacade(_config);
    }

    [Fact]
    public void CapDeposit_ShouldBeTrue()
    {
        _sut.CapDeposit.ShouldBeTrue();
    }

    [Fact]
    public void CapFullSensor_ShouldBeTrue()
    {
        _sut.CapFullSensor.ShouldBeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CapRealTimeData_ShouldReflectConfig(bool expected)
    {
        _config.Simulation.CapRealTimeData = expected;
        _sut.CapRealTimeData.ShouldBe(expected);
    }

    [Fact]
    public void CapStatisticsReporting_ShouldBeTrue()
    {
        _sut.CapStatisticsReporting.ShouldBeTrue();
    }
}
