using System.Diagnostics;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Device.Virtual;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>ハードウェアシミュレータの動作を検証するテストクラス。</summary>
public class HardwareSimulatorTests
{
    /// <summary>シミュレータが設定された遅延時間分待機することを検証する。</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task SimulateDispenseAsyncShouldDelayByConfiguredAmount()
    {
        // Arrange
        var configProvider = new ConfigurationProvider();
        configProvider.Config.Simulation = new SimulationSettings { DispenseDelayMs = 200 };
        var simulator = HardwareSimulator.Create(configProvider);
        var sw = new Stopwatch();

        // Act
        sw.Start();
        await simulator.SimulateDispenseAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        sw.Stop();

        // Assert: 少なくとも設定値（200ms）に近い時間経過していること
        // 余裕を見て150ms以上としているのは、環境によるタイマーのブレを考慮
        sw.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(150);
        sw.ElapsedMilliseconds.ShouldBeLessThan(1000); // 異常に長くないこと
    }

    /// <summary>遅延時間が0の場合、即座に完了することを検証する。</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task SimulateDispenseAsyncShouldCompleteImmediatelyWhenDelayIsZero()
    {
        // Arrange
        var configProvider = new ConfigurationProvider();
        configProvider.Config.Simulation = new SimulationSettings { DispenseDelayMs = 0 };
        var simulator = HardwareSimulator.Create(configProvider);
        var sw = new Stopwatch();

        // Act
        sw.Start();
        await simulator.SimulateDispenseAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        sw.Stop();

        // Assert: 即座に完了すること
        sw.ElapsedMilliseconds.ShouldBeLessThan(50);
    }

    /// <summary>コンストラクタによりデフォルト設定でシミュレータが初期化されることを検証する。</summary>
    [Fact]
    public void ConstructorShouldInitializeWithDefaultConfig()
    {
        // Act
        using var simulator = HardwareSimulator.Create();

        // Assert
        simulator.ShouldNotBeNull();
    }

    /// <summary>外部設定プロバイダーを使用している場合にオブジェクトの破棄が正しく行われることを検証する。</summary>
    [Fact]
    public void DisposeShouldHandleExternalConfig()
    {
        // Arrange
        var configProvider = new ConfigurationProvider();
        var simulator = HardwareSimulator.Create(configProvider);

        // Act
        simulator.Dispose();

        // Assert: configProvider should NOT be disposed because it was external
        Should.NotThrow(() => { var _ = configProvider.Config; });
    }
}
