using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Coordination;
using Microsoft.PointOfService;
using Moq;
using Shouldly;
using CashChangerSimulator.Core.Opos;

namespace CashChangerSimulator.Tests.Device;

/// <summary>テスト用のモックキャッシュチェンジャーマネージャー。</summary>
/// <param name="inv">在庫オブジェクト。</param>
public class MockCashChangerManager(Inventory inv) : CashChangerManager(inv, new TransactionHistory(), new ChangeCalculator())
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

/// <summary>テスト用のシミュレータキャッシュチェンジャー。</summary>
public class TestSimulatorCashChanger : InternalSimulatorCashChanger
{
    public List<EventArgs> QueuedEvents { get; } = [];

    public TestSimulatorCashChanger(Inventory inv, CashChangerManager manager, IDeviceSimulator? deviceSimulator = null)
        : this(inv, manager, new HardwareStatusManager(), deviceSimulator)
    {
    }

    private TestSimulatorCashChanger(Inventory inv, CashChangerManager manager, HardwareStatusManager hw, IDeviceSimulator? deviceSimulator = null)
        : base(new SimulatorDependencies(
            new ConfigurationProvider(),
            inv,
            new TransactionHistory(),
            manager,
            new DepositController(inv, hw),
            new DispenseController(manager, hw, deviceSimulator ?? new Mock<IDeviceSimulator>().Object),
            new OverallStatusAggregatorProvider(new MonitorsProvider(inv, new ConfigurationProvider(), new CurrencyMetadataProvider(new ConfigurationProvider()))),
            hw))
    {
    }

    protected override void NotifyEvent(EventArgs e)
    {
        QueuedEvents.Add(e);
        base.NotifyEvent(e);
    }
}

/// <summary>非同期モードでの出金操作（DispenseChange, DispenseCash）の挙動を検証するテストクラス。</summary>
[Collection("GlobalLock")]
public class DispenseAsyncTests
{
    /// <summary>非同期の払出操作が呼び出し元をブロックせず、完了時にイベントを発火することを検証する。</summary>
    [Fact]
    public async Task AsyncDispenseShouldNotBlockAndFireEvent()
    {
        // Arrange
        var inventory = new Inventory();
        inventory.SetCount(new DenominationKey(100, CurrencyCashType.Coin), 10);
        var manager = new MockCashChangerManager(inventory);
        var changer = new TestSimulatorCashChanger(inventory, manager)
        {
            AsyncMode = true,
        };
        changer.SkipStateVerification = false;

        changer.Open();
        changer.Claim(1000);
        changer.DeviceEnabled = true;

        // Act
        changer.DispenseChange(100);

        // Assert: Immediate return, event should not have fired yet because we haven't set the finish signal
        changer.QueuedEvents.Any(e => e is StatusUpdateEventArgs se && se.Status == 91).ShouldBeFalse();

        // Let it finish
        manager.DispenseFinishSignal.Set();

        // Wait for completion
        int timeout = 0;
        bool eventFired = false;
        while (!eventFired && timeout < 50)
        {
            await Task.Delay(TestTimingConstants.EventPropagationDelayMs * 2, TestContext.Current.CancellationToken);
            lock (changer.QueuedEvents)
            {
                eventFired = changer.QueuedEvents.Any(e => e is StatusUpdateEventArgs se && se.Status == 91);
            }
            timeout++;
        }

        eventFired.ShouldBeTrue();
    }

    /// <summary>非同期払出中に重ねて払出を要求した場合に E_BUSY がスローされることを検証します。</summary>
    [Fact]
    public async Task DispenseDuringAsyncShouldThrowBusy()
    {
        // Arrange
        var inventory = new Inventory();
        inventory.SetCount(new DenominationKey(100, CurrencyCashType.Coin), 10);
        var manager = new MockCashChangerManager(inventory);
        var changer = new TestSimulatorCashChanger(inventory, manager)
        {
            AsyncMode = true,
        };
        changer.SkipStateVerification = false;

        changer.Open();
        changer.Claim(1000);
        changer.DeviceEnabled = true;

        // Act: Start dispense but don't finish it
        var dispenseTask = Task.Run(() => changer.DispenseChange(100), TestContext.Current.CancellationToken);

        // Wait until it enters the manager
        manager.DispenseStartSignal.Wait(2000, TestContext.Current.CancellationToken).ShouldBeTrue("Dispense did not start in time");

        // Assert: While busy, another dispense should throw E_BUSY
        var ex = Should.Throw<PosControlException>(() => changer.DispenseChange(50));
        ex.ErrorCode.ShouldBe(ErrorCode.Busy);

        // Cleanup
        manager.DispenseFinishSignal.Set();
        await dispenseTask;
    }

    /// <summary>非同期払出中に在庫読取を試みた場合に E_BUSY がスローされることを検証します。</summary>
    [Fact]
    public async Task ReadCountsDuringAsyncShouldThrowBusy()
    {
        // Arrange
        var inventory = new Inventory();
        inventory.SetCount(new DenominationKey(100, CurrencyCashType.Coin), 10);
        var manager = new MockCashChangerManager(inventory);
        var changer = new TestSimulatorCashChanger(inventory, manager)
        {
            AsyncMode = true,
        };
        changer.SkipStateVerification = false;

        changer.Open();
        changer.Claim(1000);
        changer.DeviceEnabled = true;

        // Act: Start dispense but don't finish it
        var dispenseTask = Task.Run(() => changer.DispenseChange(100), TestContext.Current.CancellationToken);
        manager.DispenseStartSignal.Wait(2000, TestContext.Current.CancellationToken).ShouldBeTrue();

        // Assert: While busy, ReadCashCounts should throw E_BUSY
        var ex = Should.Throw<PosControlException>(() => changer.ReadCashCounts());
        ex.ErrorCode.ShouldBe(ErrorCode.Busy);

        // Cleanup
        manager.DispenseFinishSignal.Set();
        await dispenseTask;
    }

    /// <summary>ClearOutput 呼び出しにより、実行中の非同期払出が適切にキャンセルされることを検証します。</summary>
    [Fact]
    public async Task ClearOutputShouldCancelAsyncDispense()
    {
        // Arrange
        var inventory = new Inventory();
        inventory.SetCount(new DenominationKey(100, CurrencyCashType.Coin), 10);
        var manager = new MockCashChangerManager(inventory);
        
        // Mock simulator that we can block and that respects the token
        var mockSimulator = new Mock<IDeviceSimulator>();
        var hardwareSimulatedSignal = new ManualResetEventSlim(false);
        
        mockSimulator.Setup(s => s.SimulateDispenseAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken ct) =>
            {
                hardwareSimulatedSignal.Set();
                try
                {
                    await Task.Delay(10000, ct); // Block until cancelled
                }
                catch (OperationCanceledException)
                {
                    // Expected
                    throw;
                }
            });

        var changer = new TestSimulatorCashChanger(inventory, manager, mockSimulator.Object)
        {
            AsyncMode = true,
        };
        changer.SkipStateVerification = false;

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
        
        // Wait a bit for the async task to propagate the cancellation catch
        await Task.Delay(TestTimingConstants.EventPropagationDelayMs * 2, TestContext.Current.CancellationToken);
        
        // Verify no AsyncFinished event fired
        lock (changer.QueuedEvents)
        {
            changer.QueuedEvents.Any(e => e is StatusUpdateEventArgs se && se.Status == 91).ShouldBeFalse("Cancelled operation should not fire AsyncFinished.");
        }
    }

    /// <summary>非同期払出時にハード故障が発生した場合、AsyncResultCodeExtended にエラー詳細がセットされることを検証します。</summary>
    [Fact]
    public async Task AsyncDispenseFailureShouldSetAsyncResultCodeExtended()
    {
        // Arrange
        var inventory = new Inventory();
        inventory.SetCount(new DenominationKey(100, CurrencyCashType.Coin), 10);
        var manager = new MockCashChangerManager(inventory);
        
        var mockSimulator = new Mock<IDeviceSimulator>();
        mockSimulator.Setup(s => s.SimulateDispenseAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PosControlException("Hardware simulated error", ErrorCode.Extended, (int)UposCashChangerErrorCodeExtended.Jam));

        var changer = new TestSimulatorCashChanger(inventory, manager, mockSimulator.Object)
        {
            AsyncMode = true,
        };
        changer.SkipStateVerification = false;

        changer.Open();
        changer.Claim(1000);
        changer.DeviceEnabled = true;

        // Act
        changer.DispenseChange(100);
        manager.DispenseFinishSignal.Set();

        // Assert: Wait for completion in QueuedEvents
        int timeout = 0;
        bool eventFired = false;
        while (!eventFired && timeout < 50)
        {
            await Task.Delay(TestTimingConstants.EventPropagationDelayMs * 2, TestContext.Current.CancellationToken);
            lock (changer.QueuedEvents)
            {
                eventFired = changer.QueuedEvents.Any(e => e is StatusUpdateEventArgs se && se.Status == 91);
            }
            timeout++;
        }

        eventFired.ShouldBeTrue();

        // Check exact extended code
        changer.AsyncResultCodeExtended.ShouldBe((int)UposCashChangerErrorCodeExtended.Jam);
    }
}
