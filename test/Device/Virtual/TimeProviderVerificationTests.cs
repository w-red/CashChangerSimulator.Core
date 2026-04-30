using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device.Virtual;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Device.Virtual;

/// <summary>TimeProvider による仮想時間制御の動作と、決定論的なテスト実行を検証するテストクラス。</summary>
public class TimeProviderVerificationTests
{
    private readonly Inventory inventory;
    private readonly HardwareStatusManager hardwareStatusManager;
    private readonly FakeTimeProvider timeProvider;

    /// <summary>テストの初期設定を行います。</summary>
    public TimeProviderVerificationTests()
    {
        inventory = Inventory.Create();
        hardwareStatusManager = HardwareStatusManager.Create();
        timeProvider = new FakeTimeProvider();
    }

    /// <summary>FakeTimeProvider を用いて EndDepositAsync が仮想時間経過後に完了することを検証します。</summary>
    [Fact]
    public async Task EndDepositAsyncWithFakeTimeProviderShouldCompleteInstantly()
    {
        // Arrange
        var config = new ConfigurationProvider();
        // デフォルトの入金遅延は設定により異なるが、通常数百ms
        var delayMs = config.Config.Simulation.DepositDelayMs;
        delayMs.ShouldBeGreaterThan(0);

        var manager = new CashChangerManager(inventory, new TransactionHistory(), config);
        var loggerFactory = new LoggerFactory();
        var simulator = new Mock<IDeviceSimulator>().Object;

        var controller = new DepositController(
            manager,
            inventory,
            hardwareStatusManager,
            config,
            loggerFactory,
            timeProvider);

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

    /// <summary>FakeTimeProvider を用いて HardwareSimulator が仮想時間経過後に完了することを検証します。</summary>
    [Fact]
    public async Task HardwareSimulatorWithFakeTimeProviderShouldCompleteInstantly()
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
