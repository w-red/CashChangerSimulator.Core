using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Device;
using Shouldly;
using System.Diagnostics;
using Xunit;

namespace CashChangerSimulator.Tests.Device;

/// <summary>ハードウェアシミュレータの動作を検証するテストクラス。</summary>
public class HardwareSimulatorTests
{
    /// <summary>シミュレータが設定された遅延時間分待機することを検証する。</summary>
    [Fact]
    public async Task SimulateDispenseAsyncShouldDelayByConfiguredAmount()
    {
        // Arrange
        var configProvider = new ConfigurationProvider();
        configProvider.Config.Simulation = new SimulationSettings { DispenseDelayMs = 200 };
        var simulator = new HardwareSimulator(configProvider);
        var sw = new Stopwatch();

        // Act
        sw.Start();
        await simulator.SimulateDispenseAsync(TestContext.Current.CancellationToken);
        sw.Stop();

        // Assert: 少なくとも設定値（200ms）に近い時間経過していること
        // 余裕を見て150ms以上としているのは、環境によるタイマーのブレを考慮
        sw.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(150);
        sw.ElapsedMilliseconds.ShouldBeLessThan(1000); // 異常に長くないこと
    }

    /// <summary>遅延時間が0の場合、即座に完了することを検証する。</summary>
    [Fact]
    public async Task SimulateDispenseAsyncShouldCompleteImmediatelyWhenDelayIsZero()
    {
        // Arrange
        var configProvider = new ConfigurationProvider();
        configProvider.Config.Simulation = new SimulationSettings { DispenseDelayMs = 0 };
        var simulator = new HardwareSimulator(configProvider);
        var sw = new Stopwatch();

        // Act
        sw.Start();
        await simulator.SimulateDispenseAsync(TestContext.Current.CancellationToken);
        sw.Stop();

        // Assert: 即座に完了すること
        sw.ElapsedMilliseconds.ShouldBeLessThan(50);
    }
}
