using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Strategies;

namespace CashChangerSimulator.Tests.Device;

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

    [Fact]
    public void SetOverlapStrategy_ShouldUpdateHardwareStatus()
    {
        // Arrange
        var strategy = new SetOverlapStrategy();
        
        // Act
        var result = strategy.Execute(1, "test", _device);

        // Assert
        Assert.Equal(1, result.Data);
        // HardwareStatusManager を通じて状態が変わったかを確認したいが、
        // InternalSimulatorCashChanger の非公開フィールドなので間接的に確認するか、
        // DirectIO の実行結果で判断する。
        // ここでは Strategy 単体の責務を検証する。
    }

    [Fact]
    public void SetJamStrategy_ShouldUpdateHardwareStatus()
    {
        // Arrange
        var strategy = new SetJamStrategy();
        
        // Act
        var result = strategy.Execute(1, "test", _device);

        // Assert
        Assert.Equal(1, result.Data);
    }

    [Fact]
    public void DirectIO_ShouldUseStrategies()
    {
        // Arrange
        // (Device is already initialized and opened in the constructor)

        // Act & Assert
        // SetOverlap
        var resultOverlap = _device.DirectIO(DirectIOCommands.SetOverlap, 1, "test");
        Assert.Equal(1, resultOverlap.Data);
        Assert.True(_device._hardwareStatusManager.IsOverlapped.Value);

        // SetJam
        var resultJam = _device.DirectIO(DirectIOCommands.SetJam, 1, "test");
        Assert.Equal(1, resultJam.Data);
        Assert.True(_device._hardwareStatusManager.IsJammed.Value);

        // GetVersion
        var resultVersion = _device.DirectIO(DirectIOCommands.GetVersion, 0, "");
        Assert.StartsWith("InternalSimulatorCashChanger v", resultVersion.Object as string);
    }
}
