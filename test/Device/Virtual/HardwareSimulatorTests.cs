using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Device.Virtual;
using Shouldly;
using System.Diagnostics;

namespace CashChangerSimulator.Tests.Device.Virtual;

/// <summary>ハードウェアシミュレータの動作を検証するテストクラス。</summary>
public class HardwareSimulatorTests : DeviceTestBase
{
    private class ConfigurationProviderScope : IDisposable
    {
        public ConfigurationProvider Content { get; } = new ConfigurationProvider();
        public void Dispose() => Content.Dispose();
    }

    /// <summary>シミュレータが設定された遅延時間分待機することを検証する。</summary>
    [Fact]
    public async Task SimulateDispenseAsyncShouldDelayByConfiguredAmount()
    {
        // Arrange
        ConfigurationProvider.Config.Simulation = new SimulationSettings { DispenseDelayMs = 200 };
        var simulator = HardwareSimulator.Create(ConfigurationProvider);
        var sw = new Stopwatch();

        // Act
        sw.Start();
        await simulator.SimulateDispenseAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        sw.Stop();

        // Assert: 少なくとも設定値(200ms)に近い時間経過していること
        // 余裕を見て150ms以上としているのは、環境によるタイマーのブレを考慮
        sw.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(150);
        sw.ElapsedMilliseconds.ShouldBeLessThan(1000); // 異常に長くないこと
    }

    /// <summary>遅延時間が0の場合、即座に完了することを検証する。</summary>
    [Fact]
    public async Task SimulateDispenseAsyncShouldCompleteImmediatelyWhenDelayIsZero()
    {
        // Arrange
        ConfigurationProvider.Config.Simulation = new SimulationSettings { DispenseDelayMs = 0 };
        var simulator = HardwareSimulator.Create(ConfigurationProvider);
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
        using var scope = new ConfigurationProviderScope();
        var simulator = HardwareSimulator.Create(scope.Content);

        // Act
        simulator.Dispose();

        // Assert: configProvider should NOT be disposed because it was external
        Should.NotThrow(() => { var _ = scope.Content.Config; });
    }
}
