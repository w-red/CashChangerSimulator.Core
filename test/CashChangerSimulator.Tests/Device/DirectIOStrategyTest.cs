using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Strategies;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>DirectIO コマンドの各戦略（Strategy）を検証するテストクラス。</summary>
public class DirectIOStrategyTest
{
    private readonly InternalSimulatorCashChanger _device;

    public DirectIOStrategyTest()
    {
        // InternalSimulatorCashChanger を初期化（SkipStateVerification = true にして OPOS API の制約を回避）
        _device = new InternalSimulatorCashChanger();
        _device.SkipStateVerification = true;
        _device.Open();
        _device.Claim(0);
    }

    /// <summary>SetOverlapStrategy が HardwareStatusManager の重なり状態を正しく更新することを検証する。</summary>
    [Fact]
    public void SetOverlapStrategyShouldUpdateHardwareStatus()
    {
        // Arrange
        var strategy = new SetOverlapStrategy();

        // Act
        var result = strategy.Execute(1, "test", _device);

        // Assert
        result.Data.ShouldBe(1);
        _device._hardwareStatusManager.IsOverlapped.Value.ShouldBeTrue();
    }

    /// <summary>SetJamStrategy が箇所指定付きでジャム状態を正しく更新することを検証する。</summary>
    [Fact]
    public void SetJamStrategyShouldUpdateHardwareStatusWithLocation()
    {
        // Arrange
        var strategy = new SetJamStrategy();

        // Act
        var result = strategy.Execute(1, "BillCassette1", _device);

        // Assert
        result.Data.ShouldBe(1);
        _device._hardwareStatusManager.IsJammed.Value.ShouldBeTrue();
        _device._hardwareStatusManager.JamLocation.Value.ShouldBe(JamLocation.BillCassette1);
    }

    /// <summary>GetJamLocation コマンドが現在のジャム箇所を文字列で返却することを検証する。</summary>
    [Fact]
    public void GetJamLocationShouldReturnCurrentLocation()
    {
        // Arrange
        _device._hardwareStatusManager.SetJammed(true, JamLocation.Inlet);

        // Act
        var result = _device.DirectIO(DirectIOCommands.GetJamLocation, 0, "");

        // Assert
        result.Object.ShouldBe("Inlet");
    }

    /// <summary>DirectIO メソッド経由で各戦略が正しく呼び出されることを検証する。</summary>
    [Fact]
    public void DirectIOShouldUseStrategies()
    {
        // Arrange & Act & Assert
        // SetOverlap
        var resultOverlap = _device.DirectIO(DirectIOCommands.SetOverlap, 1, "test");
        resultOverlap.Data.ShouldBe(1);
        _device._hardwareStatusManager.IsOverlapped.Value.ShouldBeTrue();

        // SetJam
        var resultJam = _device.DirectIO(DirectIOCommands.SetJam, 1, "Transport");
        resultJam.Data.ShouldBe(1);
        _device._hardwareStatusManager.IsJammed.Value.ShouldBeTrue();
        _device._hardwareStatusManager.JamLocation.Value.ShouldBe(JamLocation.Transport);

        // GetVersion
        var resultVersion = _device.DirectIO(DirectIOCommands.GetVersion, 0, "");
        resultVersion.Object.ToString().ShouldStartWith("InternalSimulatorCashChanger v");
    }
}
