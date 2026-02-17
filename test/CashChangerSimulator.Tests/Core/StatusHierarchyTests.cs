using CashChangerSimulator.Core.Models;
using MoneyKind4Opos.Currencies.Interfaces;
using Shouldly;

namespace CashChangerSimulator.Tests.Core;

public class StatusHierarchyTests
{
    /// <summary>空の状態とフルの状態が別々のスロットで発生した際、双方が正しく報告されることを検証する。</summary>
    [Fact]
    public void StatusHierarchyEmptyShouldNotBeMaskedByFullInDifferentSlots()
    {
        // Arrange
        var inventory = new Inventory();
        var d1 = new DenominationKey(1000, CashType.Bill);
        var d2 = new DenominationKey(5000, CashType.Bill);
        
        var monitors = new List<CashStatusMonitor>
        {
            new CashStatusMonitor(inventory, d1, nearEmptyThreshold: 2, nearFullThreshold: 8, fullThreshold: 10),
            new CashStatusMonitor(inventory, d2, nearEmptyThreshold: 2, nearFullThreshold: 8, fullThreshold: 10)
        };

        var aggregator = new OverallStatusAggregator(monitors);
        
        // Act: d1 is Empty, d2 is Full
        inventory.SetCount(d1, 0);
        inventory.SetCount(d2, 10);

        // Assert: Both properties should report their respective worst states
        aggregator.DeviceStatus.CurrentValue.ShouldBe(CashStatus.Empty);
        aggregator.FullStatus.CurrentValue.ShouldBe(CashStatus.Full);
        
        // Act: d1 is Normal, d2 is NearFull
        inventory.SetCount(d1, 5);
        inventory.SetCount(d2, 9);
        
        aggregator.DeviceStatus.CurrentValue.ShouldBe(CashStatus.Normal);
        aggregator.FullStatus.CurrentValue.ShouldBe(CashStatus.NearFull);

        // Act: d1 is NearEmpty, d2 is NearFull
        inventory.SetCount(d1, 1);
        
        aggregator.DeviceStatus.CurrentValue.ShouldBe(CashStatus.NearEmpty);
        aggregator.FullStatus.CurrentValue.ShouldBe(CashStatus.NearFull);
    }
}
