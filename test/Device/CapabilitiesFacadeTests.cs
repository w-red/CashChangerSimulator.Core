using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Device.PosForDotNet.Facades;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>デバイスの能力(Capabilities)を公開するファサードを検証するテストクラス。</summary>
public class CapabilitiesFacadeTests
{
    private readonly SimulatorConfiguration config;
    private readonly CapabilitiesFacade sut;

    public CapabilitiesFacadeTests()
    {
        config = new SimulatorConfiguration();
        sut = new CapabilitiesFacade(config);
    }

    /// <summary>入金能力(CapDeposit)が常に真であることを検証します。</summary>
    [Fact]
    public void CapDepositShouldBeTrue()
    {
        CapabilitiesFacade.CapDeposit.ShouldBeTrue();
    }

    /// <summary>満杯センサー能力(CapFullSensor)が常に真であることを検証します。</summary>
    [Fact]
    public void CapFullSensorShouldBeTrue()
    {
        CapabilitiesFacade.CapFullSensor.ShouldBeTrue();
    }

    /// <summary>リアルタイムデータ能力(CapRealTimeData)が設定を反映することを検証します。</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CapRealTimeDataShouldReflectConfig(bool expected)
    {
        config.Simulation.CapRealTimeData = expected;
        sut.CapRealTimeData.ShouldBe(expected);
    }

    /// <summary>統計報告能力(CapStatisticsReporting)が常に真であることを検証します。</summary>
    [Fact]
    public void CapStatisticsReportingShouldBeTrue()
    {
        CapabilitiesFacade.CapStatisticsReporting.ShouldBeTrue();
    }
}