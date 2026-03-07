using CashChangerSimulator.Core.Models;
using Shouldly;

namespace CashChangerSimulator.Tests.Core;

public class InventoryTests
{
    private readonly Inventory _inventory = new();

    [Fact]
    public void Add_ShouldIncreaseCount()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        _inventory.Add(key, 5);
        _inventory.GetCount(key).ShouldBe(5);
    }

    [Fact]
    public void Add_NegativeShouldDecreaseCount()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        _inventory.SetCount(key, 10);
        _inventory.Add(key, -3);
        _inventory.GetCount(key).ShouldBe(7);
    }

    [Fact]
    public void Add_ResultingInNegative_ShouldBeZero()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        _inventory.SetCount(key, 5);
        _inventory.Add(key, -10);
        _inventory.GetCount(key).ShouldBe(0);
    }

    [Fact]
    public void SetCount_ShouldOverwriteCount()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        _inventory.SetCount(key, 10);
        _inventory.SetCount(key, 5);
        _inventory.GetCount(key).ShouldBe(5);
    }

    [Fact]
    public void SetCount_Negative_ShouldBeIgnored()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        _inventory.SetCount(key, 10);
        _inventory.SetCount(key, -5);
        _inventory.GetCount(key).ShouldBe(10);
    }

    [Fact]
    public void AddCollection_ShouldIncreaseCollectionCount()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        _inventory.AddCollection(key, 5);
        _inventory.CollectionCounts.ShouldContain(kv => kv.Key == key && kv.Value == 5);
        _inventory.HasDiscrepancy.ShouldBeTrue();
    }

    [Fact]
    public void AddCollection_ResultingInNegative_ShouldBeZero()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        _inventory.AddCollection(key, 5);
        _inventory.AddCollection(key, -10);
        _inventory.CollectionCounts.ShouldContain(kv => kv.Key == key && kv.Value == 0);
    }

    [Fact]
    public void AddReject_ShouldIncreaseRejectCount()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        _inventory.AddReject(key, 5);
        _inventory.RejectCounts.ShouldContain(kv => kv.Key == key && kv.Value == 5);
        _inventory.HasDiscrepancy.ShouldBeTrue();
    }

    [Fact]
    public void AddReject_ResultingInNegative_ShouldBeZero()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        _inventory.AddReject(key, 5);
        _inventory.AddReject(key, -10);
        _inventory.RejectCounts.ShouldContain(kv => kv.Key == key && kv.Value == 0);
    }

    [Fact]
    public void CalculateTotal_ShouldIncludeAllSources()
    {
        var bill1000 = new DenominationKey(1000, CurrencyCashType.Bill);
        var coin500 = new DenominationKey(500, CurrencyCashType.Coin);

        _inventory.Add(bill1000, 1);       // 1000
        _inventory.AddCollection(bill1000, 1); // 1000
        _inventory.AddReject(coin500, 1);     // 500

        _inventory.CalculateTotal().ShouldBe(2500);
    }

    [Fact]
    public void Clear_ShouldResetAllCounts()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        _inventory.Add(key, 1);
        _inventory.AddCollection(key, 1);
        _inventory.AddReject(key, 1);

        _inventory.Clear();

        _inventory.GetCount(key).ShouldBe(0);
        _inventory.CollectionCounts.ShouldBeEmpty();
        _inventory.RejectCounts.ShouldBeEmpty();
        _inventory.HasDiscrepancy.ShouldBeFalse();
    }
}
