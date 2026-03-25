using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Device.Facades;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>デバイスの機能フラグ（Capabilities）が構成設定を正しく反映しているか検証するテストクラス。</summary>
public class CapabilitiesFacadeTests
{
    private readonly SimulatorConfiguration _config;
    private readonly CapabilitiesFacade _sut;

    public CapabilitiesFacadeTests()
    {
        _config = new SimulatorConfiguration();
        _sut = new CapabilitiesFacade(_config);
    }

    /// <summary>入金機能（CapDeposit）が有効であることを確認します。</summary>
    [Fact]
    public void CapDeposit_ShouldBeTrue()
    {
        _sut.CapDeposit.ShouldBeTrue();
    }

    /// <summary>フルセンサー機能（CapFullSensor）が有効であることを確認します。</summary>
    [Fact]
    public void CapFullSensor_ShouldBeTrue()
    {
        _sut.CapFullSensor.ShouldBeTrue();
    }

    /// <summary>リアルタイムデータ機能（CapRealTimeData）が構成設定を正しく反映することを検証します。</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CapRealTimeData_ShouldReflectConfig(bool expected)
    {
        _config.Simulation.CapRealTimeData = expected;
        _sut.CapRealTimeData.ShouldBe(expected);
    }

    /// <summary>統計情報報告機能（CapStatisticsReporting）が有効であることを確認します。</summary>
    [Fact]
    public void CapStatisticsReporting_ShouldBeTrue()
    {
        _sut.CapStatisticsReporting.ShouldBeTrue();
    }
}
