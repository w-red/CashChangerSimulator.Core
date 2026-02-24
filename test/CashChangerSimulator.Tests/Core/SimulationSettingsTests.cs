using CashChangerSimulator.Core.Configuration;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Tests.Core;

/// <summary>シミュレーション設定の動作を検証するテストクラス。</summary>
public class SimulationSettingsTests
{
    /// <summary>SimulationSettings がデフォルト値を保持していることを検証する。</summary>
    [Fact]
    public void SimulationSettingsShouldMaintainDefaultValues()
    {
        var settings = new SimulationSettings();
        settings.DispenseDelayMs.ShouldBe(500);
    }

    /// <summary>SimulationSettings にカスタム値を設定できることを検証する。</summary>
    [Fact]
    public void SimulationSettingsShouldStoreCustomValues()
    {
        var settings = new SimulationSettings { DispenseDelayMs = 1000 };
        settings.DispenseDelayMs.ShouldBe(1000);
    }
}
