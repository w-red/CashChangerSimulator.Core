using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

public class MockCashChangerManager(Inventory inv) : CashChangerManager(inv, new TransactionHistory())
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

public class TestSimulatorCashChanger(Inventory inv, CashChangerManager manager) : SimulatorCashChanger(null, inv, null, manager, null, null)
{
    public List<EventArgs> QueuedEvents { get; } = [];

    protected override void NotifyEvent(EventArgs e)
    {
        lock (QueuedEvents)
        {
            QueuedEvents.Add(e);
        }
        // base.NotifyEvent(e); // Don't call base to avoid framework issues in unit tests
    }
}

public class DispenseAsyncTests
{
    /// <summary>非同期の払出操作が呼び出し元をブロックせず、完了時にイベントを発火することを検証する。</summary>
    [Fact]
    public async Task AsyncDispenseShouldNotBlockAndFireEvent()
    {
        // Arrange
        var inventory = new Inventory();
        inventory.SetCount(new DenominationKey(100, MoneyKind4Opos.Currencies.Interfaces.CashType.Coin), 10);
        var manager = new MockCashChangerManager(inventory);
        var changer = new TestSimulatorCashChanger(inventory, manager)
        {
            AsyncMode = true
        };

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
            await Task.Delay(100, TestContext.Current.CancellationToken);
            lock (changer.QueuedEvents)
            {
                eventFired = changer.QueuedEvents.Any(e => e is StatusUpdateEventArgs se && se.Status == 91);
            }
            timeout++;
        }

        eventFired.ShouldBeTrue();
    }

    /// <summary>非同期払出の実行中に別の払出操作を試みた際、ErrorCode.Busy がスローされることを検証する。</summary>
    [Fact]
    public async Task DispenseDuringAsyncShouldThrowBusy()
    {
        // Arrange
        var inventory = new Inventory();
        inventory.SetCount(new DenominationKey(100, MoneyKind4Opos.Currencies.Interfaces.CashType.Coin), 10);
        var manager = new MockCashChangerManager(inventory);
        var changer = new TestSimulatorCashChanger(inventory, manager)
        {
            AsyncMode = true
        };

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

    /// <summary>非同期払出の実行中に在庫読み取りを試みた際、ErrorCode.Busy がスローされることを検証する。</summary>
    [Fact]
    public async Task ReadCountsDuringAsyncShouldThrowBusy()
    {
        // Arrange
        var inventory = new Inventory();
        inventory.SetCount(new DenominationKey(100, MoneyKind4Opos.Currencies.Interfaces.CashType.Coin), 10);
        var manager = new MockCashChangerManager(inventory);
        var changer = new TestSimulatorCashChanger(inventory, manager)
        {
            AsyncMode = true
        };

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
}
