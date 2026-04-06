using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Device.PosForDotNet;
using CashChangerSimulator.Device.PosForDotNet.Strategies;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>DirectIO コマンドの各戦略（Strategy）を検証するテストクラス。.</summary>
public class DirectIOStrategyTest
{
    private readonly InternalSimulatorCashChanger device;

    public DirectIOStrategyTest()
    {
        // InternalSimulatorCashChanger を初期化（SkipStateVerification = true にして OPOS API の制約を回避）
        device = new InternalSimulatorCashChanger
        {
            SkipStateVerification = true
        };
        device.Open();
        device.Claim(0);
    }

    /// <summary>SetOverlapStrategy が HardwareStatus の重なり状態を正しく更新することを検証する。.</summary>
    [Fact]
    public void SetOverlapStrategyShouldUpdateHardwareStatus()
    {
        // Arrange
        var strategy = new SetOverlapStrategy();

        // Act
        var result = strategy.Execute(1, "test", device);

        // Assert
        result.Data.ShouldBe(1);
        device.HardwareStatus.IsOverlapped.Value.ShouldBeTrue();
    }

    /// <summary>SetJamStrategy が箇所指定付きでジャム状態を正しく更新することを検証する。.</summary>
    [Fact]
    public void SetJamStrategyShouldUpdateHardwareStatusWithLocation()
    {
        // Arrange
        var strategy = new SetJamStrategy();

        // Act
        var result = strategy.Execute(1, "BillCassette1", device);

        // Assert
        result.Data.ShouldBe(1);
        device.HardwareStatus.IsJammed.Value.ShouldBeTrue();
        device.HardwareStatus.JamLocation.Value.ShouldBe(JamLocation.BillCassette1);
    }

    /// <summary>GetJamLocation コマンドが現在のジャム箇所を文字列で返却することを検証する。.</summary>
    [Fact]
    public void GetJamLocationShouldReturnCurrentLocation()
    {
        // Arrange
        device.HardwareStatus.SetJammed(true, JamLocation.Inlet);

        // Act
        var result = device.DirectIO(DirectIOCommands.GetJamLocation, 0, string.Empty);

        // Assert
        result.Object.ShouldBe("Inlet");
    }

    /// <summary>DirectIO メソッド経由で各戦略が正しく呼び出されることを検証する。.</summary>
    [Fact]
    public void DirectIOShouldUseStrategies()
    {
        // Arrange & Act & Assert
        // SetOverlap
        var resultOverlap = device.DirectIO(DirectIOCommands.SetOverlap, 1, "test");
        resultOverlap.Data.ShouldBe(1);
        device.HardwareStatus.IsOverlapped.Value.ShouldBeTrue();

        // SetJam
        var resultJam = device.DirectIO(DirectIOCommands.SetJam, 1, "Transport");
        resultJam.Data.ShouldBe(1);
        device.HardwareStatus.IsJammed.Value.ShouldBeTrue();
        device.HardwareStatus.JamLocation.Value.ShouldBe(JamLocation.Transport);

        // GetVersion
        var resultVersion = device.DirectIO(DirectIOCommands.GetVersion, 0, string.Empty);
        resultVersion.Object.ToString().ShouldStartWith("InternalSimulatorCashChanger v");
    }
}
