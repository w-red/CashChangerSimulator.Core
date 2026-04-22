using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Transactions;
using Shouldly;

namespace CashChangerSimulator.Tests.Core.Models;

/// <summary>非還流ENon-Recyclable)めE�E金専用(Deposit-only)の設定における動作を検証するチE��トクラス</summary>
public class DepositOnlyTests
{
    /// <summary>入金不可(IsDepositable=false)に設定された金種が、�E金時に無視されることを検証します</summary>
    [Fact]
    public void DepositWhenIsDepositableIsFalseShouldNotAddAnyCount()
    {
        // Arrange
        var inventory = Inventory.Create();
        var configProvider = new ConfigurationProvider();
        var b2000 = new DenominationKey(2000, CurrencyCashType.Bill);

        // IsDepositable = false に設宁E
        configProvider.Config.Inventory["JPY"].Denominations["B2000"].IsDepositable = false;

        var manager = new CashChangerManager(inventory, new TransactionHistory(), configProvider);
        var counts = new Dictionary<DenominationKey, int> { { b2000, 1 } };

        // Act
        manager.Deposit(counts);

        // Assert
        inventory.GetCount(b2000).ShouldBe(0);
        inventory.CollectionCounts.ShouldBeEmpty();
        inventory.CalculateTotal().ShouldBe(0m);
    }

    /// <summary>入金可能だが非邁E��EIsRecyclable=false)の設定時、�E金�Eが回収庫へ振り�Eけられることを検証します</summary>
    [Fact]
    public void DepositWhenIsRecyclableIsFalseButIsDepositableIsTrueShouldGoToCollection()
    {
        // Arrange
        var inventory = Inventory.Create();
        var configProvider = new ConfigurationProvider();
        var b2000 = new DenominationKey(2000, CurrencyCashType.Bill);

        // 非還流だが�E金�E可能
        configProvider.Config.Inventory["JPY"].Denominations["B2000"].IsRecyclable = false;
        configProvider.Config.Inventory["JPY"].Denominations["B2000"].IsDepositable = true;

        var manager = new CashChangerManager(inventory, new TransactionHistory(), configProvider);
        var counts = new Dictionary<DenominationKey, int> { { b2000, 1 } };

        // Act
        manager.Deposit(counts);

        // Assert
        inventory.GetCount(b2000).ShouldBe(0);
        inventory.CollectionCounts.ShouldContain(kv => kv.Key == b2000 && kv.Value == 1);
        inventory.CalculateTotal().ShouldBe(2000m);
    }
}


