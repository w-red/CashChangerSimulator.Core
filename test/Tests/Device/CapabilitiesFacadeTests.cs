using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Device.PosForDotNet.Facades;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>デバイスの機能フラグ（Capabilities）が構成設定を正しく反映しているか検証するテストクラス。.</summary>
public class CapabilitiesFacadeTests
{
    private readonly SimulatorConfiguration config;
    private readonly CapabilitiesFacade sut;

    public CapabilitiesFacadeTests()
    {
        config = new SimulatorConfiguration();
        sut = new CapabilitiesFacade(config);
    }

    /// <summary>入金機能（CapDeposit）が有効であることを確認します。.</summary>
    [Fact]
    public void CapDepositShouldBeTrue()
    {
        sut.CapDeposit.ShouldBeTrue();
    }

    /// <summary>フルセンサー機能（CapFullSensor）が有効であることを確認します。.</summary>
    [Fact]
    public void CapFullSensorShouldBeTrue()
    {
        sut.CapFullSensor.ShouldBeTrue();
    }

    /// <summary>リアルタイムデータ機能（CapRealTimeData）が構成設定を正しく反映することを検証します。.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CapRealTimeDataShouldReflectConfig(bool expected)
    {
        config.Simulation.CapRealTimeData = expected;
        sut.CapRealTimeData.ShouldBe(expected);
    }

    /// <summary>統計情報報告機能（CapStatisticsReporting）が有効であることを確認します。.</summary>
    [Fact]
    public void CapStatisticsReportingShouldBeTrue()
    {
        sut.CapStatisticsReporting.ShouldBeTrue();
    }
}
