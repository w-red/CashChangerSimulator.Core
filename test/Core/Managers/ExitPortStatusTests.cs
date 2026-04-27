using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using Shouldly;
using R3;

namespace CashChangerSimulator.Tests.Core.Managers;

/// <summary>HardwareStatusManager における出金口（Exit Port）の状態管理を検証するテスト。</summary>
public class ExitPortStatusTests
{
    private readonly HardwareStatusManager manager = HardwareStatusManager.Create();

    [Fact]
    public void ExitPortCountsShouldBeInitiallyEmpty()
    {
        var countsNormal = manager.State.GetExitPortCounts(ExitPort.Normal);
        var countsCollection = manager.State.GetExitPortCounts(ExitPort.Collection);

        countsNormal.ShouldBeEmpty();
        countsCollection.ShouldBeEmpty();
    }

    [Fact]
    public void IsRemainingShouldBeInitiallyFalse()
    {
        manager.State.IsBillRemainingNormal.CurrentValue.ShouldBeFalse();
        manager.State.IsCoinRemainingNormal.CurrentValue.ShouldBeFalse();
        manager.State.IsBillRemainingCollection.CurrentValue.ShouldBeFalse();
        manager.State.IsCoinRemainingCollection.CurrentValue.ShouldBeFalse();
    }

    [Fact]
    public void AddExitPortCountsShouldUpdateCountsAndStatus()
    {
        var bill = new DenominationKey(1000, CurrencyCashType.Bill);
        var coin = new DenominationKey(100, CurrencyCashType.Coin);
        var counts = new Dictionary<DenominationKey, int>
        {
            { bill, 1 },
            { coin, 2 }
        };

        manager.Input.AddExitPortCounts(ExitPort.Normal, counts);

        var currentCounts = manager.State.GetExitPortCounts(ExitPort.Normal);
        currentCounts[bill].ShouldBe(1);
        currentCounts[coin].ShouldBe(2);

        manager.State.IsBillRemainingNormal.CurrentValue.ShouldBeTrue();
        manager.State.IsCoinRemainingNormal.CurrentValue.ShouldBeTrue();
    }

    [Fact]
    public void ClearExitPortShouldResetCountsAndStatus()
    {
        var bill = new DenominationKey(1000, CurrencyCashType.Bill);
        manager.Input.AddExitPortCounts(ExitPort.Normal, new Dictionary<DenominationKey, int> { { bill, 1 } });
        manager.State.IsBillRemainingNormal.CurrentValue.ShouldBeTrue();

        manager.Input.ClearExitPort(ExitPort.Normal);

        manager.State.GetExitPortCounts(ExitPort.Normal).ShouldBeEmpty();
        manager.State.IsBillRemainingNormal.CurrentValue.ShouldBeFalse();
    }

    [Fact]
    public void StatusUpdateEventsShouldFireWhenRemainingChanges()
    {
        int callCount = 0;
        int lastStatus = 0;
        using var d = manager.State.StatusUpdateEvents.Subscribe(e =>
        {
            callCount++;
            lastStatus = e.Status;
        });

        var bill = new DenominationKey(1000, CurrencyCashType.Bill);
        manager.Input.AddExitPortCounts(ExitPort.Normal, new Dictionary<DenominationKey, int> { { bill, 1 } });

        callCount.ShouldBeGreaterThan(0);
        lastStatus.ShouldBe(ExitPortStatusEvents.StatusBillRemainingNormal);
    }
}
