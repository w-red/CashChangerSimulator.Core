using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device.PosForDotNet;
using CashChangerSimulator.Device.PosForDotNet.Models;
using CashChangerSimulator.Device.Virtual;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.PointOfService;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>・ｽe・ｽX・ｽg・ｽp・ｽﾌ・ｿｽ・ｽb・ｽN・ｽL・ｽ・ｽ・ｽb・ｽV・ｽ・ｽ・ｽ`・ｽF・ｽ・ｽ・ｽW・ｽ・ｽ・ｽ[・ｽ}・ｽl・ｽ[・ｽW・ｽ・ｽ・ｽ[・ｽB</summary>
/// <param name="inv">・ｽﾝ庫オ・ｽu・ｽW・ｽF・ｽN・ｽg・ｽB</param>
/// <param name="config">・ｽﾝ抵ｿｽv・ｽ・ｽ・ｽo・ｽC・ｽ_・ｽ[・ｽB</param>
public class MockCashChangerManager(Inventory inv, ConfigurationProvider? config = null) : CashChangerManager(inv, new TransactionHistory(), config)
{
    public ManualResetEventSlim DispenseStartSignal { get; } = new(false);
    public ManualResetEventSlim DispenseFinishSignal { get; } = new(false);

    public override void Dispense(decimal amount, string? currencyCode = null)
    {
        DispenseStartSignal.Set();
        DispenseFinishSignal.Wait();
        base.Dispense(amount, currencyCode);
    }
}

/// <summary>・ｽe・ｽX・ｽg・ｽp・ｽﾌシ・ｽ~・ｽ・ｽ・ｽ・ｽ・ｽ[・ｽ^・ｽL・ｽ・ｽ・ｽb・ｽV・ｽ・ｽ・ｽ`・ｽF・ｽ・ｽ・ｽW・ｽ・ｽ・ｽ[・ｽB</summary>
public class TestSimulatorCashChanger : InternalSimulatorCashChanger
{
    public List<EventArgs> QueuedEvents { get; } = [];

    public TestSimulatorCashChanger(Inventory inv, CashChangerManager manager, IDeviceSimulator? deviceSimulator = null, TimeProvider? timeProvider = null, ConfigurationProvider? config = null, ILoggerFactory? loggerFactory = null)
        : this(inv, manager, HardwareStatusManager.Create(), deviceSimulator, timeProvider, config, loggerFactory)
    {
    }

    private TestSimulatorCashChanger(Inventory inv, CashChangerManager manager, HardwareStatusManager hw, IDeviceSimulator? deviceSimulator = null, TimeProvider? timeProvider = null, ConfigurationProvider? config = null, ILoggerFactory? loggerFactory = null)
        : base(new SimulatorDependencies(
            config ?? new ConfigurationProvider(),
            inv,
            new TransactionHistory(),
            manager,
            new DepositController(manager, inv, hw, config ?? new ConfigurationProvider(), loggerFactory ?? NullLoggerFactory.Instance, timeProvider),
            new DispenseController(manager, inv, config ?? new ConfigurationProvider(), loggerFactory ?? NullLoggerFactory.Instance, hw, deviceSimulator ?? new Mock<IDeviceSimulator>().Object),
            new OverallStatusAggregatorProvider(MonitorsProvider.Create(inv, config ?? new ConfigurationProvider(), CurrencyMetadataProvider.Create(config ?? new ConfigurationProvider()))),
            hw,
            TimeProvider: timeProvider))
    {
    }

    protected override void NotifyEvent(EventArgs e)
    {
        lock (QueuedEvents)
        {
            QueuedEvents.Add(e);
        }

        base.NotifyEvent(e);
    }
}

/// <summary>・ｽ�ｯ奇ｿｽ・ｽ・ｽ・ｽ[・ｽh・ｽﾅの出・ｽ・ｽ・ｽ・ｽ・ｽ・ｽ(DispenseChange, DispenseCash)・ｽﾌ具ｿｽ・ｽ・ｽ・ｽ・ｽ・ｽ・ｽ・ｽﾘゑｿｽ・ｽ・ｽe・ｽX・ｽg・ｽN・ｽ・ｽ・ｽX・ｽB</summary>
[Collection("GlobalLock")]
public class DispenseAsyncTests
{
    /// <summary>・ｽ�ｯ奇ｿｽ・ｽﾌ包ｿｽ・ｽo・ｽ・ｽ・ｽ・ｪ・ｽﾄび出・ｽ・ｽ・ｽ・ｽ・ｽ・ｽ・ｽu・ｽ・ｽ・ｽb・ｽN・ｽ・ｽ・ｽ・ｽ・ｽA・ｽ・ｽ・ｽ・ｽ・ｽ・ｽ・ｽﾉイ・ｽx・ｽ・ｽ・ｽg・ｽ�ｭ火ゑｿｽ・ｽ驍ｱ・ｽﾆゑｿｽ・ｽ・ｽ・ｽﾘゑｿｽ・ｽ・ｽB</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task AsyncDispenseShouldNotBlockAndFireEvent()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var inventory = Inventory.Create();
        inventory.SetCount(new DenominationKey(100, CurrencyCashType.Coin), 10);
        var manager = new MockCashChangerManager(inventory);
        var changer = new TestSimulatorCashChanger(inventory, manager, timeProvider: timeProvider)
        {
            AsyncMode = true,
            SkipStateVerification = false
        };

        changer.Open();
        changer.Claim(1000);
        changer.DeviceEnabled = true;

        // Mock simulator to control when it finishes
        var dispenseTcs = new TaskCompletionSource();
        Mock.Get(changer.DispenseController.Simulator).Setup(s => s.SimulateDispenseAsync(It.IsAny<CancellationToken>())).Returns(dispenseTcs.Task);

        // Act
        changer.DispenseChange(100);

        // Assert: Immediate return, event should not have fired yet because simulator is still "running"
        lock (changer.QueuedEvents)
        {
            changer.QueuedEvents.Any(e => e is StatusUpdateEventArgs se && se.Status == (int)UposCashChangerStatusUpdateCode.AsyncFinished).ShouldBeFalse();
        }

        // Let simulator finish
        dispenseTcs.SetResult();
        timeProvider.Advance(TimeSpan.FromMilliseconds(20));

        // Let manager finish
        manager.DispenseFinishSignal.Set();

        // Wait for completion event
        await WaitUntil(() =>
        {
            lock (changer.QueuedEvents)
            {
                return changer.QueuedEvents.Any(e => e is StatusUpdateEventArgs se && se.Status == (int)UposCashChangerStatusUpdateCode.AsyncFinished);
            }
        }, timeProvider: timeProvider);
    }

    /// <summary>・ｽ�ｯ奇ｿｽ・ｽ・ｽ・ｽo・ｽ・ｽ・ｽﾉ重・ｽﾋて包ｿｽ・ｽo・ｽ・ｽv・ｽ・ｽ・ｽ・ｽ・ｽ・ｽ・ｽ鼾・ｿｽ・ｽ E_BUSY ・ｽ・ｽ・ｽX・ｽ・ｽ・ｽ[・ｽ・ｽ・ｽ・ｽ驍ｱ・ｽﾆゑｿｽ・ｽ・ｽ・ｽﾘゑｿｽ・ｽﾜゑｿｽ・ｽB</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task DispenseDuringAsyncShouldThrowBusy()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var inventory = Inventory.Create();
        var denomination = new DenominationKey(100, CurrencyCashType.Coin);
        inventory.SetCount(denomination, 10);

        var config = new ConfigurationProvider();
        config.Config.Inventory["JPY"].Denominations[denomination.ToDenominationString()] = new DenominationSettings { IsRecyclable = true };

        var manager = new MockCashChangerManager(inventory, config);
        var changer = new TestSimulatorCashChanger(inventory, manager, timeProvider: timeProvider, config: config)
        {
            AsyncMode = true,
            SkipStateVerification = false
        };

        changer.Open();
        changer.Claim(1000);
        changer.DeviceEnabled = true;

        // Ensure the mock simulator stays busy until we say so
        var dispenseTcs = new TaskCompletionSource();
        Mock.Get(changer.DispenseController.Simulator).Setup(s => s.SimulateDispenseAsync(It.IsAny<CancellationToken>())).Returns(dispenseTcs.Task);

        // Act: Start dispense (should return immediately because AsyncMode = true)
        changer.DispenseChange(100);

        // Wait until it becomes busy.
        await WaitUntil(() => changer.DispenseController.IsBusy, timeProvider: timeProvider);

        // Assert: While busy, another dispense should throw E_BUSY
        var ex = Should.Throw<PosControlException>(() => changer.DispenseChange(50));
        ex.ErrorCode.ShouldBe(ErrorCode.Busy);

        // Cleanup: Finish the first dispense
        dispenseTcs.SetResult();
        timeProvider.Advance(TimeSpan.FromMilliseconds(20)); // Advance to complete Task.Delay in simulator

        manager.DispenseFinishSignal.Set();
        await WaitUntil(() => !changer.DispenseController.IsBusy, timeProvider: timeProvider);
    }

    private async Task WaitUntil(Func<bool> condition, int timeoutSeconds = 5, TimeProvider? timeProvider = null)
    {
        var tp = timeProvider ?? TimeProvider.System;
        var startTimestamp = tp.GetTimestamp();
        var timeoutTicks = TimeSpan.FromSeconds(timeoutSeconds).Ticks;

        while (!condition())
        {
            var elapsedTicks = tp.GetTimestamp() - startTimestamp;
            if (elapsedTicks > timeoutTicks)
            {
                condition().ShouldBeTrue($"Condition was not met within {timeoutSeconds}s (virtual time)");
                return;
            }

            if (tp is FakeTimeProvider ftp)
            {
                ftp.Advance(TimeSpan.FromMilliseconds(5));
            }

            // [STABILITY] Use a small real delay to ensure background tasks are scheduled 
            // on the current thread's synchronization context/scheduler.
            await Task.Delay(1);
        }
    }
    /// <summary>・ｽ�ｯ奇ｿｽ・ｽ・ｽ・ｽo・ｽ・ｽ・ｽﾉ在庫読趣ｿｽ・ｽ・ｽ・ｽ・ｽﾝゑｿｽ・ｽ鼾・ｿｽ・ｽ E_BUSY ・ｽ・ｽ・ｽX・ｽ・ｽ・ｽ[・ｽ・ｽ・ｽ・ｽ驍ｱ・ｽﾆゑｿｽ・ｽ・ｽ・ｽﾘゑｿｽ・ｽﾜゑｿｽ・ｽB</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task ReadCountsDuringAsyncShouldThrowBusy()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var inventory = Inventory.Create();
        inventory.SetCount(new DenominationKey(100, CurrencyCashType.Coin), 10);
        var manager = new MockCashChangerManager(inventory);
        var changer = new TestSimulatorCashChanger(inventory, manager, timeProvider: timeProvider)
        {
            AsyncMode = true,
            SkipStateVerification = false
        };

        changer.Open();
        changer.Claim(1000);
        changer.DeviceEnabled = true;

        // Act: Start dispense but don't finish it
        var dispenseTcs = new TaskCompletionSource();
        Mock.Get(changer.DispenseController.Simulator).Setup(s => s.SimulateDispenseAsync(It.IsAny<CancellationToken>())).Returns(dispenseTcs.Task);

        changer.DispenseChange(100);
        await WaitUntil(() => changer.DispenseController.IsBusy, timeProvider: timeProvider);

        // Assert: While busy, ReadCashCounts should throw E_BUSY
        var ex = Should.Throw<PosControlException>(() => changer.ReadCashCounts());
        ex.ErrorCode.ShouldBe((ErrorCode)DeviceErrorCode.Busy);

        // Cleanup
        dispenseTcs.SetResult();
        timeProvider.Advance(TimeSpan.FromMilliseconds(20));
        manager.DispenseFinishSignal.Set();
        await WaitUntil(() => !changer.DispenseController.IsBusy, timeProvider: timeProvider);
    }

    /// <summary>ClearOutput ・ｽﾄび出・ｽ・ｽ・ｽﾉゑｿｽ・ｽA・ｽ・ｽ・ｽs・ｽ・ｽ・ｽﾌ非同奇ｿｽ・ｽ・ｽ・ｽo・ｽ・ｽ・ｽK・ｽﾘにキ・ｽ・ｽ・ｽ・ｽ・ｽZ・ｽ・ｽ・ｽ・ｽ・ｽ・ｽ驍ｱ・ｽﾆゑｿｽ・ｽ・ｽ・ｽﾘゑｿｽ・ｽﾜゑｿｽ・ｽB</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task ClearOutputShouldCancelAsyncDispense()
    {
        // Arrange
        var inventory = Inventory.Create();
        inventory.SetCount(new DenominationKey(100, CurrencyCashType.Coin), 10);
        var manager = new MockCashChangerManager(inventory);

        // Mock simulator that we can block and that respects the token
        var timeProvider = new FakeTimeProvider();
        var mockSimulator = new Mock<IDeviceSimulator>();
        var hardwareSimulatedSignal = new ManualResetEventSlim(false);

        mockSimulator.Setup(s => s.SimulateDispenseAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken ct) =>
            {
                hardwareSimulatedSignal.Set();
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), timeProvider, ct).ConfigureAwait(false); // Block until cancelled
                }
                catch (OperationCanceledException)
                {
                    // Expected
                    throw;
                }
            });

        var changer = new TestSimulatorCashChanger(inventory, manager, mockSimulator.Object, timeProvider)
        {
            AsyncMode = true,
            SkipStateVerification = false
        };

        changer.Open();
        changer.Claim(1000);
        changer.DeviceEnabled = true;

        // Act: Start dispense
        changer.DispenseChange(100);

        // Wait until it enters the simulator
        hardwareSimulatedSignal.Wait(2000, TestContext.Current.CancellationToken).ShouldBeTrue("Hardware simulation did not start");

        // Assert: It should be busy
        var exBusy = Should.Throw<PosControlException>(() => changer.DispenseChange(50));
        exBusy.ErrorCode.ShouldBe(ErrorCode.Busy);

        // Act: Clear Output
        changer.ClearOutput();

        // Assert: Should be Idle again
        changer.ReadCashCounts(); // Should NO LONGER throw Busy

        // Propagate virtual time to let internal tasks catch up if any
        timeProvider.Advance(TimeSpan.FromMilliseconds(100));
        await Task.Yield(); // Simple yield instead of real-time delay

        // Verify no AsyncFinished event fired
        lock (changer.QueuedEvents)
        {
            changer.QueuedEvents.Any(e => e is StatusUpdateEventArgs se && se.Status == (int)UposCashChangerStatusUpdateCode.AsyncFinished).ShouldBeFalse("Cancelled operation should not fire AsyncFinished.");
        }
    }

    /// <summary>・ｽ�ｯ奇ｿｽ・ｽ・ｽ・ｽo・ｽ・ｽ・ｽﾉハ・ｽ[・ｽh・ｽﾌ障が・ｽ・ｽ・ｽ・ｽ・ｽ・ｽ・ｽ・ｽ・ｽ鼾・ｿｽAAsyncResultCodeExtended ・ｽﾉエ・ｽ・ｽ・ｽ[・ｽﾚ細ゑｿｽ・ｽZ・ｽb・ｽg・ｽ・ｽ・ｽ・ｽ驍ｱ・ｽﾆゑｿｽ・ｽ・ｽ・ｽﾘゑｿｽ・ｽﾜゑｿｽ・ｽB</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task AsyncDispenseFailureShouldSetAsyncResultCodeExtended()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var inventory = Inventory.Create();
        inventory.SetCount(new DenominationKey(100, CurrencyCashType.Coin), 10);
        var manager = new MockCashChangerManager(inventory);

        var mockSimulator = new Mock<IDeviceSimulator>();
        mockSimulator.Setup(s => s.SimulateDispenseAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PosControlException("Hardware simulated error", ErrorCode.Extended, 202));

        var changer = new TestSimulatorCashChanger(inventory, manager, mockSimulator.Object, timeProvider)
        {
            AsyncMode = true,
            SkipStateVerification = false
        };

        changer.Open();
        changer.Claim(1000);
        changer.DeviceEnabled = true;

        // Act
        changer.DispenseChange(100);

        manager.DispenseFinishSignal.Set();

        await WaitUntil(() =>
        {
            lock (changer.QueuedEvents)
            {
                return changer.QueuedEvents.Any(e => e is StatusUpdateEventArgs se && se.Status == (int)UposCashChangerStatusUpdateCode.AsyncFinished);
            }
        }, timeoutSeconds: 5, timeProvider: timeProvider);

        // Assert
        // Check exact extended code
        changer.AsyncResultCodeExtended.ShouldBe(202);
    }
}


