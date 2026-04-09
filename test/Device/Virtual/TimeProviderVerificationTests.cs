using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Device.Virtual;

/// <summary>TimeProvider による仮想時間制御の動作と、決定論的なテスト実行を検証するテストクラス。</summary>
public class TimeProviderVerificationTests
{
    private readonly Inventory inventory;
    private readonly HardwareStatusManager hardwareStatusManager;
    private readonly FakeTimeProvider timeProvider;

    public TimeProviderVerificationTests()
    {
        inventory = Inventory.Create();
        hardwareStatusManager = HardwareStatusManager.Create();
        timeProvider = new FakeTimeProvider();
    }

    [Fact]
    public async Task EndDepositAsync_WithFakeTimeProvider_ShouldCompleteInstantly()
    {
        // Arrange
        var config = new ConfigurationProvider();
        // デフォルトの入金遅延は設定により異なるが、通常数百ms
        var delayMs = config.Config.Simulation.DepositDelayMs;
        delayMs.ShouldBeGreaterThan(0);

        var controller = new DepositController(
            inventory,
            hardwareStatusManager,
            timeProvider: timeProvider);

        controller.BeginDeposit();
        // 直接 DenominationKey を生成
        controller.TrackDeposit(new DenominationKey(1000, CurrencyCashType.Bill));
        controller.FixDeposit();

        // Act
        var task = controller.EndDepositAsync(DepositAction.NoChange);

        // まだ終わっていないはず (仮想時間で 0ms しか経過していないため)
        task.IsCompleted.ShouldBeFalse();

        // 仮想時間を進める
        timeProvider.Advance(TimeSpan.FromMilliseconds(delayMs));

        // Assert
        await task; // ここで即座に完了するはず
        task.IsCompleted.ShouldBeTrue();
        controller.DepositStatus.ShouldBe(DeviceDepositStatus.End);
    }

    [Fact]
    public async Task HardwareSimulator_WithFakeTimeProvider_ShouldCompleteInstantly()
    {
        // Arrange
        var config = new ConfigurationProvider();
        var delayMs = config.Config.Simulation.DispenseDelayMs;
        delayMs.ShouldBeGreaterThan(0);

        var simulator = HardwareSimulator.Create(config, timeProvider);

        // Act
        var task = simulator.SimulateDispenseAsync();

        // まだ終わっていないはず
        task.IsCompleted.ShouldBeFalse();

        // 仮想時間を進める
        timeProvider.Advance(TimeSpan.FromMilliseconds(delayMs));

        // Assert
        await task;
        task.IsCompleted.ShouldBeTrue();
    }
}
