using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Device.Virtual;
using Shouldly;
using System.Diagnostics;

namespace CashChangerSimulator.Tests.Device.Virtual;

/// <summary>ハードウェアシミュレータの動作を検証するテストクラス。</summary>
[Collection("SequentialHardwareTests")]
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

    /// <summary>カスタム TimeProvider が正しく注入されることを検証する。</summary>
    [Fact]
    public void Create_WithCustomTimeProvider_ShouldUseIt()
    {
        // Arrange
        var mockTime = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();

        // Act
        using var simulator = HardwareSimulator.Create(mockTime);

        // Assert: 内部フィールドの検証は困難だが、動作で確認可能
        var sw = Stopwatch.StartNew();
        var task = simulator.SimulateDispenseAsync();
        
        // Advance time
        mockTime.Advance(TimeSpan.FromSeconds(1));
        
        // Assert
        task.IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public void DispenseDelayMs_ShouldThrow_WhenNegative()
    {
        // Arrange
        var settings = new SimulationSettings();

        // Act & Assert
        Should.Throw<ArgumentOutOfRangeException>(() => settings.DispenseDelayMs = -1);
    }

    /// <summary>TimeProvider に null を渡した場合、TimeProvider.System が使用されることを確認します。</summary>
    [Fact]
    public void Create_WithNullTimeProvider_ShouldUseSystemTime()
    {
        // Act
        using var simulator = HardwareSimulator.Create(timeProvider: null);

        // Assert: 内部フィールドの検証は困難だが、呼び出しが例外にならないことを確認
        Should.NotThrow(() => simulator.SimulateDispenseAsync());
    }
}
