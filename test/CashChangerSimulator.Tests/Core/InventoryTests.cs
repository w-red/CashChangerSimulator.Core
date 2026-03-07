using CashChangerSimulator.Core.Models;
using Shouldly;

namespace CashChangerSimulator.Tests.Core;

public class InventoryTests
{
    private readonly Inventory Inventory = new();

    [Fact]
    public void Add_ShouldIncreaseCount()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        Inventory.Add(key, 5);
        Inventory.GetCount(key).ShouldBe(5);
    }

    [Fact]
    public void Add_NegativeShouldDecreaseCount()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        Inventory.SetCount(key, 10);
        Inventory.Add(key, -3);
        Inventory.GetCount(key).ShouldBe(7);
    }

    [Fact]
    public void Add_ResultingInNegative_ShouldBeZero()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        Inventory.SetCount(key, 5);
        Inventory.Add(key, -10);
        Inventory.GetCount(key).ShouldBe(0);
    }

    [Fact]
    public void SetCount_ShouldOverwriteCount()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        Inventory.SetCount(key, 10);
        Inventory.SetCount(key, 5);
        Inventory.GetCount(key).ShouldBe(5);
    }

    [Fact]
    public void SetCount_Negative_ShouldBeIgnored()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        Inventory.SetCount(key, 10);
        Inventory.SetCount(key, -5);
        Inventory.GetCount(key).ShouldBe(10);
    }

    [Fact]
    public void AddCollection_ShouldIncreaseCollectionCount()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        Inventory.AddCollection(key, 5);
        Inventory.CollectionCounts.ShouldContain(kv => kv.Key == key && kv.Value == 5);
        Inventory.HasDiscrepancy.ShouldBeTrue();
    }

    [Fact]
    public void AddCollection_ResultingInNegative_ShouldBeZero()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        Inventory.AddCollection(key, 5);
        Inventory.AddCollection(key, -10);
        Inventory.CollectionCounts.ShouldContain(kv => kv.Key == key && kv.Value == 0);
    }

    [Fact]
    public void AddReject_ShouldIncreaseRejectCount()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        Inventory.AddReject(key, 5);
        Inventory.RejectCounts.ShouldContain(kv => kv.Key == key && kv.Value == 5);
        Inventory.HasDiscrepancy.ShouldBeTrue();
    }

    [Fact]
    public void AddReject_ResultingInNegative_ShouldBeZero()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        Inventory.AddReject(key, 5);
        Inventory.AddReject(key, -10);
        Inventory.RejectCounts.ShouldContain(kv => kv.Key == key && kv.Value == 0);
    }

    [Fact]
    public void CalculateTotal_ShouldIncludeAllSources()
    {
        var bill1000 = new DenominationKey(1000, CurrencyCashType.Bill);
        var coin500 = new DenominationKey(500, CurrencyCashType.Coin);

        Inventory.Add(bill1000, 1);       // 1000
        Inventory.AddCollection(bill1000, 1); // 1000
        Inventory.AddReject(coin500, 1);     // 500

        Inventory.CalculateTotal().ShouldBe(2500);
    }

    [Fact]
    public void Clear_ShouldResetAllCounts()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        Inventory.Add(key, 1);
        Inventory.AddCollection(key, 1);
        Inventory.AddReject(key, 1);

        Inventory.Clear();

        Inventory.GetCount(key).ShouldBe(0);
        Inventory.CollectionCounts.ShouldBeEmpty();
        Inventory.RejectCounts.ShouldBeEmpty();
        Inventory.HasDiscrepancy.ShouldBeFalse();
    }
}
