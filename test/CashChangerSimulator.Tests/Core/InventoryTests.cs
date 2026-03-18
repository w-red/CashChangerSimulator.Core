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
    public void Add_Zero_ShouldDoNothing()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        Inventory.Add(key, 0);
        Inventory.GetCount(key).ShouldBe(0);
    }

    [Fact]
    public void AddCollection_Zero_ShouldDoNothing()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        Inventory.AddCollection(key, 0);
        Inventory.CollectionCounts.ShouldBeEmpty();
    }

    [Fact]
    public void AddReject_Zero_ShouldDoNothing()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        Inventory.AddReject(key, 0);
        Inventory.RejectCounts.ShouldBeEmpty();
    }

    [Fact]
    public void NormalizeKey_ShouldSetDefaultCurrencyIfEmpty()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill, "");
        Inventory.Add(key, 1);
        Inventory.GetCount(key).ShouldBe(1);
        Inventory.AllCounts.First().Key.CurrencyCode.ShouldBe(DenominationKey.DefaultCurrencyCode);
    }

    [Fact]
    public void CalculateTotal_WithCurrencyCode_ShouldOnlyIncludeMatches()
    {
        var jpy1000 = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        var usd1 = new DenominationKey(1, CurrencyCashType.Bill, "USD");

        Inventory.Add(jpy1000, 1);
        Inventory.Add(usd1, 1);

        Inventory.CalculateTotal("JPY").ShouldBe(1000);
        Inventory.CalculateTotal("USD").ShouldBe(1);
        Inventory.CalculateTotal().ShouldBe(1001);
    }

    [Fact]
    public void Dictionary_Serialization_ShouldWork()
    {
        var pay = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        Inventory.Add(pay, 1);
        Inventory.AddCollection(pay, 2);
        Inventory.AddReject(pay, 3);

        var dict = Inventory.ToDictionary();
        dict.Count.ShouldBe(3);
        dict["JPY:B1000"].ShouldBe(1);
        dict["COL:JPY:B1000"].ShouldBe(2);
        dict["REJ:JPY:B1000"].ShouldBe(3);

        var newInventory = new Inventory();
        newInventory.LoadFromDictionary(dict);
        newInventory.GetCount(pay).ShouldBe(1);
        newInventory.CollectionCounts.First(kv => kv.Key == pay).Value.ShouldBe(2);
        newInventory.RejectCounts.First(kv => kv.Key == pay).Value.ShouldBe(3);
    }

    [Fact]
    public void LoadFromDictionary_InvalidKey_ShouldIgnore()
    {
        var dict = new Dictionary<string, int> { { "INVALID", 10 } };
        Inventory.LoadFromDictionary(dict); // Should not throw
        Inventory.CalculateTotal().ShouldBe(0);
    }

    [Fact]
    public void SetDiscrepancy_ShouldWork()
    {
        Inventory.HasDiscrepancy.ShouldBeFalse();
        Inventory.HasDiscrepancy = true;
        Inventory.HasDiscrepancy.ShouldBeTrue();
    }
    
    [Fact]
    public void AddEscrow_ShouldIncreaseEscrowCount()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        Inventory.AddEscrow(key, 5); // Should fail to compile/run initially
        Inventory.EscrowCounts.ShouldContain(kv => kv.Key == key && kv.Value == 5);
    }

    [Fact]
    public void ClearEscrow_ShouldResetEscrowCount()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        Inventory.AddEscrow(key, 5);
        Inventory.ClearEscrow();
        Inventory.EscrowCounts.ShouldBeEmpty();
    }

    [Fact]
    public void CalculateTotal_ShouldIncludeEscrow()
    {
        var bill1000 = new DenominationKey(1000, CurrencyCashType.Bill);
        Inventory.Add(bill1000, 1);       // 1000
        Inventory.AddEscrow(bill1000, 2); // 2000
        Inventory.CalculateTotal().ShouldBe(3000);
    }
}
