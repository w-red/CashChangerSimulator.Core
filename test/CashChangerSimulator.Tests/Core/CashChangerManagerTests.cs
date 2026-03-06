
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using Shouldly;

namespace CashChangerSimulator.Tests.Core;
/// <summary>CashChangerManager のビジネスロジックを検証するテスト。</summary>
public class CashChangerManagerTests
{
    /// <summary>入金時に在庫と履歴が正しく更新されることを検証する。</summary>
    [Fact]
    public void DepositShouldUpdateInventoryAndHistory()
    {
        // Arrange
        var inventory = new Inventory();
        var history = new TransactionHistory();
        var manager = new CashChangerManager(inventory, history, new ChangeCalculator());
        var b1000 = new DenominationKey(1000, CurrencyCashType.Bill);
        var c100 = new DenominationKey(100, CurrencyCashType.Coin);
        var depositCounts = new Dictionary<DenominationKey, int> { { b1000, 2 }, { c100, 5 } };

        // Act
        manager.Deposit(depositCounts);

        // Assert: Inventory updated
        inventory.GetCount(b1000).ShouldBe(2);
        inventory.GetCount(c100).ShouldBe(5);
        inventory.CalculateTotal().ShouldBe(2500m);

        // Assert: History added
        history.Entries.Count.ShouldBe(1);
        var entry = history.Entries[0];
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
        var inventory = new Inventory();
        var b1000 = new DenominationKey(1000, CurrencyCashType.Bill);
        inventory.SetCount(b1000, 10);
        var history = new TransactionHistory();
        var manager = new CashChangerManager(inventory, history, new ChangeCalculator());
        var dispenseCounts = new Dictionary<DenominationKey, int> { { b1000, 3 } };

        // Act
        manager.Dispense(dispenseCounts);

        // Assert: Inventory updated
        inventory.GetCount(b1000).ShouldBe(7);

        // Assert: History added
        history.Entries.Count.ShouldBe(1);
        var entry = history.Entries[0];
        entry.Type.ShouldBe(TransactionType.Dispense);
        entry.Amount.ShouldBe(3000m);
    }

    /// <summary>金額指定の払出時に計算、更新、記録が正しく行われることを検証する。</summary>
    [Fact]
    public void DispenseByAmountShouldCalculateUpdateAndRecord()
    {
        // Arrange
        var inventory = new Inventory();
        var b1000 = new DenominationKey(1000, CurrencyCashType.Bill);
        var c100 = new DenominationKey(100, CurrencyCashType.Coin);
        inventory.SetCount(b1000, 5);
        inventory.SetCount(c100, 10);
        var history = new TransactionHistory();
        var manager = new CashChangerManager(inventory, history, new ChangeCalculator());

        // Act: 1200円を出金
        manager.Dispense(1200m);

        // Assert: Inventory updated
        inventory.GetCount(b1000).ShouldBe(4);
        inventory.GetCount(c100).ShouldBe(8);

        // Assert: History recorded
        history.Entries.Count.ShouldBe(1);
        history.Entries[0].Amount.ShouldBe(1200m);
        history.Entries[0].Counts[b1000].ShouldBe(1);
        history.Entries[0].Counts[c100].ShouldBe(2);
    }

    /// <summary>多通貨（USD）での入金と履歴記録を検証する。</summary>
    [Fact]
    public void DepositWithOtherCurrencyShouldStoreCorrectCurrencyCode()
    {
        // Arrange
        var inventory = new Inventory();
        var history = new TransactionHistory();
        var manager = new CashChangerManager(inventory, history, new ChangeCalculator());
        var usd20 = new DenominationKey(20, CurrencyCashType.Bill, "USD");
        var depositCounts = new Dictionary<DenominationKey, int> { { usd20, 2 } };

        // Act
        manager.Deposit(depositCounts);

        // Assert
        inventory.GetCount(usd20).ShouldBe(2);
        inventory.CalculateTotal("USD").ShouldBe(40m);
        history.Entries[0].Counts.Keys.ShouldContain(k => k.CurrencyCode == "USD");
    }

    /// <summary>在庫不足時に払出が失敗し、在庫と履歴が更新されないことを検証する。</summary>
    [Fact]
    public void DispenseByAmountWithInsufficientCashShouldThrowAndNotModifyState()
    {
        // Arrange
        var inventory = new Inventory(); // Empty
        var history = new TransactionHistory();
        var manager = new CashChangerManager(inventory, history, new ChangeCalculator());

        // Act & Assert
        Should.Throw<InsufficientCashException>(() => manager.Dispense(100m));

        inventory.CalculateTotal().ShouldBe(0m);
        history.Entries.ShouldBeEmpty();
    }

    /// <summary>非リサイクル金種が入金時に回収庫へ振り分けられることを検証する。</summary>
    [Fact]
    public void DepositNonRecyclableShouldGoToCollection()
    {
        // Arrange
        var inventory = new Inventory();
        var configProvider = new ConfigurationProvider();
        // 2000円札を非リサイクルとして明示的に設定
        configProvider.Config.Inventory["JPY"].Denominations["B2000"].IsRecyclable = false;
        
        var manager = new CashChangerManager(inventory, new TransactionHistory(), new ChangeCalculator(), configProvider);
        
        var b2000 = new DenominationKey(2000, CurrencyCashType.Bill);
        var counts = new Dictionary<DenominationKey, int> { { b2000, 3 } };

        // Act
        manager.Deposit(counts);

        // Assert
        inventory.GetCount(b2000).ShouldBe(0); // 通常庫は0
        inventory.CollectionCounts.ShouldContain(kv => kv.Key == b2000 && kv.Value == 3);
        inventory.CalculateTotal().ShouldBe(6000m);
    }

    /// <summary>金額指定の出金時に非リサイクル金種がスキップされることを検証する。</summary>
    [Fact]
    public void DispenseByAmountShouldSkipNonRecyclable()
    {
        // Arrange
        var inventory = new Inventory();
        var b2000 = new DenominationKey(2000, CurrencyCashType.Bill);
        var b1000 = new DenominationKey(1000, CurrencyCashType.Bill);
        
        // 2000円札（非リサイクル）と1000円札（リサイクル）を在庫に入れる
        inventory.SetCount(b2000, 10);
        inventory.SetCount(b1000, 10);
        
        var configProvider = new ConfigurationProvider();
        // 2000円札を非リサイクルとして明示的に設定
        configProvider.Config.Inventory["JPY"].Denominations["B2000"].IsRecyclable = false;
        
        var manager = new CashChangerManager(inventory, new TransactionHistory(), new ChangeCalculator(), configProvider);

        // Act: 2000円を出金
        manager.Dispense(2000m);

        // Assert: 1000円札2枚が使われ、2000円札は使われないはず
        inventory.GetCount(b1000).ShouldBe(8);
        inventory.GetCount(b2000).ShouldBe(10);
    }

    /// <summary>入金時に満廃しきい値を超えた分が回収庫へ振り分けられることを検証する。</summary>
    [Fact]
    public void DepositShouldHandleOverflow()
    {
        // Arrange
        var inventory = new Inventory();
        var b1000 = new DenominationKey(1000, CurrencyCashType.Bill);
        
        // 1000円札の Full しきい値はデフォルトで 100 枚とする
        // 現状の在庫を 90 枚にする
        inventory.SetCount(b1000, 90);
        
        var configProvider = new ConfigurationProvider();
        var manager = new CashChangerManager(inventory, new TransactionHistory(), new ChangeCalculator(), configProvider);
        var counts = new Dictionary<DenominationKey, int> { { b1000, 20 } };

        // Act: 20枚入金 (計 110枚)
        manager.Deposit(counts);

        // Assert: 100枚までが通常庫、残り10枚が回収庫
        inventory.GetCount(b1000).ShouldBe(100);
        inventory.CollectionCounts.ShouldContain(kv => kv.Key == b1000 && kv.Value == 10);
    }
}
