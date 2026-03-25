using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using Shouldly;

namespace CashChangerSimulator.Tests.Core;

/// <summary>非還流（Non-Recyclable）や入金専用（Deposit-only）の設定における動作を検証するテストクラス。</summary>
public class DepositOnlyTests
{
    /// <summary>入金不可（IsDepositable=false）に設定された金種が、入金時に無視されることを検証します。</summary>
    [Fact]
    public void Deposit_WhenIsDepositableIsFalse_ShouldNotAddAnyCount()
    {
        // Arrange
        var inventory = new Inventory();
        var configProvider = new ConfigurationProvider();
        var b2000 = new DenominationKey(2000, CurrencyCashType.Bill);
        
        // IsDepositable = false に設定
        configProvider.Config.Inventory["JPY"].Denominations["B2000"].IsDepositable = false;
        
        var manager = new CashChangerManager(inventory, new TransactionHistory(), new ChangeCalculator(), configProvider);
        var counts = new Dictionary<DenominationKey, int> { { b2000, 1 } };

        // Act
        manager.Deposit(counts);

        // Assert
        inventory.GetCount(b2000).ShouldBe(0);
        inventory.CollectionCounts.ShouldBeEmpty();
        inventory.CalculateTotal().ShouldBe(0m);
    }

    /// <summary>入金可能だが非還流（IsRecyclable=false）の設定時、入金分が回収庫へ振り分けられることを検証します。</summary>
    [Fact]
    public void Deposit_WhenIsRecyclableIsFalseButIsDepositableIsTrue_ShouldGoToCollection()
    {
        // Arrange
        var inventory = new Inventory();
        var configProvider = new ConfigurationProvider();
        var b2000 = new DenominationKey(2000, CurrencyCashType.Bill);
        
        // 非還流だが入金は可能
        configProvider.Config.Inventory["JPY"].Denominations["B2000"].IsRecyclable = false;
        configProvider.Config.Inventory["JPY"].Denominations["B2000"].IsDepositable = true;

        var manager = new CashChangerManager(inventory, new TransactionHistory(), new ChangeCalculator(), configProvider);
        var counts = new Dictionary<DenominationKey, int> { { b2000, 1 } };

        // Act
        manager.Deposit(counts);

        // Assert
        inventory.GetCount(b2000).ShouldBe(0);
        inventory.CollectionCounts.ShouldContain(kv => kv.Key == b2000 && kv.Value == 1);
        inventory.CalculateTotal().ShouldBe(2000m);
    }
}
