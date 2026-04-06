using CashChangerSimulator.Core.Models;
using Shouldly;

namespace CashChangerSimulator.Tests.Core;

/// <summary>在庫管理（Inventory）の加算、設定、集計、シリアライズ機能を検証するためのテストクラス。</summary>
public class InventoryTests
{
    private readonly Inventory inventory = new();

    /// <summary>指定された金種の数量を増加させることができることを検証する。</summary>
    [Fact]
    public void AddShouldIncreaseCount()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        inventory.Add(key, 5);
        inventory.GetCount(key).ShouldBe(5);
    }

    /// <summary>負の値を加算した際に数量が減少することを検証する。</summary>
    [Fact]
    public void AddNegativeShouldDecreaseCount()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        inventory.SetCount(key, 10);
        inventory.Add(key, -3);
        inventory.GetCount(key).ShouldBe(7);
    }

    /// <summary>加算の結果が負になる場合に数量が0になることを検証する。</summary>
    [Fact]
    public void AddResultingInNegativeShouldBeZero()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        inventory.SetCount(key, 5);
        inventory.Add(key, -10);
        inventory.GetCount(key).ShouldBe(0);
    }

    /// <summary>指定された金種の数量を直接設定できることを検証する。</summary>
    [Fact]
    public void SetCountShouldOverwriteCount()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        inventory.SetCount(key, 10);
        inventory.SetCount(key, 5);
        inventory.GetCount(key).ShouldBe(5);
    }

    /// <summary>負の数量を設定しようとした際に、操作が無視されることを検証する。</summary>
    [Fact]
    public void SetCountNegativeShouldBeIgnored()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        inventory.SetCount(key, 10);
        inventory.SetCount(key, -5);
        inventory.GetCount(key).ShouldBe(10);
    }

    /// <summary>回収庫の数量を増加させることができることを検証する。</summary>
    [Fact]
    public void AddCollectionShouldIncreaseCollectionCount()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        inventory.AddCollection(key, 5);
        inventory.CollectionCounts.ShouldContain(kv => kv.Key == key && kv.Value == 5);
        inventory.HasDiscrepancy.ShouldBeTrue();
    }

    /// <summary>回収庫の加算結果が負になる場合に数量が0になることを検証する。</summary>
    [Fact]
    public void AddCollectionResultingInNegativeShouldBeZero()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        inventory.AddCollection(key, 5);
        inventory.AddCollection(key, -10);
        inventory.CollectionCounts.ShouldContain(kv => kv.Key == key && kv.Value == 0);
    }

    /// <summary>リジェクト庫の数量を増加させることができることを検証する。</summary>
    [Fact]
    public void AddRejectShouldIncreaseRejectCount()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        inventory.AddReject(key, 5);
        inventory.RejectCounts.ShouldContain(kv => kv.Key == key && kv.Value == 5);
        inventory.HasDiscrepancy.ShouldBeTrue();
    }

    /// <summary>リジェクト庫の加算結果が負になる場合に数量が0になることを検証する。</summary>
    [Fact]
    public void AddRejectResultingInNegativeShouldBeZero()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        inventory.AddReject(key, 5);
        inventory.AddReject(key, -10);
        inventory.RejectCounts.ShouldContain(kv => kv.Key == key && kv.Value == 0);
    }

    /// <summary>通常庫、回収庫、リジェクト庫の合計金額を正しく計算できることを検証する。</summary>
    [Fact]
    public void CalculateTotalShouldIncludeAllSources()
    {
        var bill1000 = new DenominationKey(1000, CurrencyCashType.Bill);
        var coin500 = new DenominationKey(500, CurrencyCashType.Coin);

        inventory.Add(bill1000, 1);       // 1000
        inventory.AddCollection(bill1000, 1); // 1000
        inventory.AddReject(coin500, 1);     // 500

        inventory.CalculateTotal().ShouldBe(2500);
    }

    /// <summary>数量0の加算操作が状態を変更しないことを検証する。</summary>
    [Fact]
    public void AddZeroShouldDoNothing()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        inventory.Add(key, 0);
        inventory.GetCount(key).ShouldBe(0);
    }

    /// <summary>回収庫への数量0の加算操作が状態を変更しないことを検証する。</summary>
    [Fact]
    public void AddCollectionZeroShouldDoNothing()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        inventory.AddCollection(key, 0);
        inventory.CollectionCounts.ShouldBeEmpty();
    }

    /// <summary>リジェクト庫への数量0の加算操作が状態を変更しないことを検証する。</summary>
    [Fact]
    public void AddRejectZeroShouldDoNothing()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        inventory.AddReject(key, 0);
        inventory.RejectCounts.ShouldBeEmpty();
    }

    /// <summary>通貨コードが空の場合にデフォルトの通貨コードが設定されることを検証する。</summary>
    [Fact]
    public void NormalizeKeyShouldSetDefaultCurrencyIfEmpty()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill, string.Empty);
        inventory.Add(key, 1);
        inventory.GetCount(key).ShouldBe(1);
        inventory.AllCounts.First().Key.CurrencyCode.ShouldBe(DenominationKey.DefaultCurrencyCode);
    }

    /// <summary>通貨コードを指定した合計金額の計算が、該当する通貨のみを集計することを検証する。</summary>
    [Fact]
    public void CalculateTotalWithCurrencyCodeShouldOnlyIncludeMatches()
    {
        var jpy1000 = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        var usd1 = new DenominationKey(1, CurrencyCashType.Bill, "USD");

        inventory.Add(jpy1000, 1);
        inventory.Add(usd1, 1);

        inventory.CalculateTotal("JPY").ShouldBe(1000);
        inventory.CalculateTotal("USD").ShouldBe(1);
        inventory.CalculateTotal().ShouldBe(1001);
    }

    /// <summary>ディクショナリ形式へのシリアライズとデシリアライズが正しく動作することを検証する。</summary>
    [Fact]
    public void DictionarySerializationShouldWork()
    {
        var pay = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        inventory.Add(pay, 1);
        inventory.AddCollection(pay, 2);
        inventory.AddReject(pay, 3);

        var dict = inventory.ToDictionary();
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

    /// <summary>不正なキーを含むディクショナリからの読み込みが、エラーなく無視されることを検証する。</summary>
    [Fact]
    public void LoadFromDictionaryInvalidKeyShouldIgnore()
    {
        var dict = new Dictionary<string, int> { { "INVALID", 10 } };
        inventory.LoadFromDictionary(dict); // Should not throw
        inventory.CalculateTotal().ShouldBe(0);
    }

    /// <summary>不一致状態（Discrepancy）の設定と取得が正しく行われることを検証する。</summary>
    [Fact]
    public void SetDiscrepancyShouldWork()
    {
        inventory.HasDiscrepancy.ShouldBeFalse();
        inventory.HasDiscrepancy = true;
        inventory.HasDiscrepancy.ShouldBeTrue();
    }

    /// <summary>エスクロー（投入中）の数量を増加させることができることを検証する。</summary>
    [Fact]
    public void AddEscrowShouldIncreaseEscrowCount()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        inventory.AddEscrow(key, 5);
        inventory.EscrowCounts.ShouldContain(kv => kv.Key == key && kv.Value == 5);
    }

    /// <summary>エスクローの数量をリセットできることを検証する。</summary>
    [Fact]
    public void ClearEscrowShouldResetEscrowCount()
    {
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        inventory.AddEscrow(key, 5);
        inventory.ClearEscrow();
        inventory.EscrowCounts.ShouldBeEmpty();
    }

    /// <summary>合計金額の計算にエスクローの金額が含まれることを検証する。</summary>
    [Fact]
    public void CalculateTotalShouldIncludeEscrow()
    {
        var bill1000 = new DenominationKey(1000, CurrencyCashType.Bill);
        inventory.Add(bill1000, 1);       // 1000
        inventory.AddEscrow(bill1000, 2); // 2000
        inventory.CalculateTotal().ShouldBe(3000);
    }
}
