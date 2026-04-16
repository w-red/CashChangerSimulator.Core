using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Transactions;
using Shouldly;

namespace CashChangerSimulator.Tests.Core.Managers;

/// <summary>CashChangerManager のビジネスロジックを検証するテスト。</summary>
public class CashChangerManagerTests : CoreTestBase
{
    /// <summary>入金時に在庫と履歴が正しく更新されることを検証する。</summary>
    [Fact]
    public void DepositShouldUpdateInventoryAndHistory()
    {
        // Arrange
        var b1000 = new DenominationKey(1000, CurrencyCashType.Bill);
        var c100 = new DenominationKey(100, CurrencyCashType.Coin);
        var depositCounts = new Dictionary<DenominationKey, int> { { b1000, 2 }, { c100, 5 } };

        // Act
        Manager.Deposit(depositCounts);

        // Assert: Inventory updated
        Inventory.GetCount(b1000).ShouldBe(2);
        Inventory.GetCount(c100).ShouldBe(5);
        Inventory.CalculateTotal().ShouldBe(2500m);

        // Assert: History added
        History.Entries.Count.ShouldBe(1);
        var entry = History.Entries[0];
        entry.Type.ShouldBe(TransactionType.Deposit);
        entry.Amount.ShouldBe(2500m);
        entry.Counts[b1000].ShouldBe(2);
        entry.Counts[c100].ShouldBe(5);
    }

    /// <summary>払出時に在庫と履歴が正しく更新されることを検証する。</summary>
    [Fact]
    public void DispenseShouldUpdateInventoryAndHistory()
    {
        // Arrange
        var b1000 = new DenominationKey(1000, CurrencyCashType.Bill);
        Inventory.SetCount(b1000, 10);
        var dispenseCounts = new Dictionary<DenominationKey, int> { { b1000, 3 } };

        // Act
        Manager.Dispense(dispenseCounts);

        // Assert: Inventory updated
        Inventory.GetCount(b1000).ShouldBe(7);

        // Assert: History added
        History.Entries.Count.ShouldBe(1);
        var entry = History.Entries[0];
        entry.Type.ShouldBe(TransactionType.Dispense);
        entry.Amount.ShouldBe(3000m);
    }

    /// <summary>金額指定の払出時に計算、在庫更新、履歴記録が正しく行われることを検証する。</summary>
    [Fact]
    public void DispenseByAmountShouldCalculateUpdateAndRecord()
    {
        // Arrange
        var b1000 = new DenominationKey(1000, CurrencyCashType.Bill);
        var c100 = new DenominationKey(100, CurrencyCashType.Coin);
        Inventory.SetCount(b1000, 5);
        Inventory.SetCount(c100, 10);

        // Act: 1200円を出金
        Manager.Dispense(1200m);

        // Assert: Inventory updated
        Inventory.GetCount(b1000).ShouldBe(4);
        Inventory.GetCount(c100).ShouldBe(8);

        // Assert: History recorded
        History.Entries.Count.ShouldBe(1);
        History.Entries[0].Amount.ShouldBe(1200m);
        History.Entries[0].Counts[b1000].ShouldBe(1);
        History.Entries[0].Counts[c100].ShouldBe(2);
    }

    /// <summary>多通貨(USD)での入金と履歴記録を検証する。</summary>
    [Fact]
    public void DepositWithOtherCurrencyShouldStoreCorrectCurrencyCode()
    {
        // Arrange
        var usd20 = new DenominationKey(20, CurrencyCashType.Bill, "USD");
        var depositCounts = new Dictionary<DenominationKey, int> { { usd20, 2 } };

        // Act
        Manager.Deposit(depositCounts);

        // Assert
        Inventory.GetCount(usd20).ShouldBe(2);
        Inventory.CalculateTotal("USD").ShouldBe(40m);
        History.Entries[0].Counts.Keys.ShouldContain(k => k.CurrencyCode == "USD");
    }

    /// <summary>在庫不足時に払出が失敗し、在庫と履歴が更新されないことを検証する。</summary>
    [Fact]
    public void DispenseByAmountWithInsufficientCashShouldThrowAndNotModifyState()
    {
        // Act & Assert
        Should.Throw<InsufficientCashException>(() => Manager.Dispense(100m));

        Inventory.CalculateTotal().ShouldBe(0m);
        History.Entries.ShouldBeEmpty();
    }

    /// <summary>非リサイクル金種が入金時に回収庫へ振り分けられることを検証する。</summary>
    [Fact]
    public void DepositNonRecyclableShouldGoToCollection()
    {
        // Arrange
        var b2000 = new DenominationKey(2000, CurrencyCashType.Bill);

        // 2000円札を非リサイクルとして明示的に設定
        ConfigurationProvider.Config.Inventory["JPY"].Denominations["B2000"].IsRecyclable = false;

        var counts = new Dictionary<DenominationKey, int> { { b2000, 3 } };

        // Act
        Manager.Deposit(counts);

        // Assert
        Inventory.GetCount(b2000).ShouldBe(0); // 通常庫は0
        Inventory.CollectionCounts.ShouldContain(kv => kv.Key == b2000 && kv.Value == 3);
        Inventory.CalculateTotal().ShouldBe(6000m);
    }

    /// <summary>金額指定の出金時に非リサイクル金種がスキップされることを検証する。</summary>
    [Fact]
    public void DispenseByAmountShouldSkipNonRecyclable()
    {
        // Arrange
        var b2000 = new DenominationKey(2000, CurrencyCashType.Bill);
        var b1000 = new DenominationKey(1000, CurrencyCashType.Bill);

        // 2000円札(非リサイクル)と1000円札(リサイクル)を在庫に入れる
        Inventory.SetCount(b2000, 10);
        Inventory.SetCount(b1000, 10);

        // 2000円札を非リサイクルとして明示的に設定
        ConfigurationProvider.Config.Inventory["JPY"].Denominations["B2000"].IsRecyclable = false;

        // Act: 2000円を出金
        Manager.Dispense(2000m);

        // Assert: 1000円札2枚が使われ、2000円札は使われないはず
        Inventory.GetCount(b1000).ShouldBe(8);
        Inventory.GetCount(b2000).ShouldBe(10);
    }

    /// <summary>入金時に満廃しきい値を超えた分が回収庫へ振り分けられることを検証する。</summary>
    [Fact]
    public void DepositShouldHandleOverflow()
    {
        // Arrange
        var b1000 = new DenominationKey(1000, CurrencyCashType.Bill);

        // 1000円札の Full しきい値はデフォルトで 100 枚とする
        // 現状の在庫を 90 枚にする
        Inventory.SetCount(b1000, 90);

        var counts = new Dictionary<DenominationKey, int> { { b1000, 20 } };

        // Act: 20枚入金 (計 110枚)
        Manager.Deposit(counts);

        // Assert: 100枚までが通常庫、残り10枚が回収庫
        Inventory.GetCount(b1000).ShouldBe(100);
        Inventory.CollectionCounts.ShouldContain(kv => kv.Key == b1000 && kv.Value == 10);
    }

    /// <summary>入金付加(IsDepositable=false)な金種がスキップされることを検証する。</summary>
    [Fact]
    public void DepositShouldSkipWhenNotDepositable()
    {
        // Arrange
        var b10000 = new DenominationKey(10000, CurrencyCashType.Bill);

        // 10000円札を入金不可に設定
        ConfigurationProvider.Config.Inventory["JPY"].Denominations["B10000"].IsDepositable = false;

        // Act
        Manager.Deposit(new Dictionary<DenominationKey, int> { { b10000, 1 } });

        // Assert
        Inventory.GetCount(b10000).ShouldBe(0);
        Inventory.CollectionCounts.ShouldBeEmpty();
    }

    /// <summary>PurgeCash がリサイクル可能な在庫をすべて回収庫へ移動することを検証する。</summary>
    [Fact]
    public void PurgeCashShouldMoveRecyclableInventoryToCollection()
    {
        // Arrange
        var b1000 = new DenominationKey(1000, CurrencyCashType.Bill);
        Inventory.SetCount(b1000, 50);

        // Act
        Manager.PurgeCash();

        // Assert
        Inventory.GetCount(b1000).ShouldBe(0);
        Inventory.CollectionCounts.First(kv => kv.Key == b1000).Value.ShouldBe(50);
        History.Entries.Count.ShouldBe(1);
        History.Entries[0].Amount.ShouldBe(50000m);
    }

    /// <summary>Adjust が在庫枚数を直接設定できることを検証する。</summary>
    [Fact]
    public void AdjustShouldSetInventoryCounts()
    {
        // Arrange
        var b1000 = new DenominationKey(1000, CurrencyCashType.Bill);

        // Act
        Manager.Adjust(new Dictionary<DenominationKey, int> { { b1000, 123 } });

        // Assert
        Inventory.GetCount(b1000).ShouldBe(123);
    }
}
