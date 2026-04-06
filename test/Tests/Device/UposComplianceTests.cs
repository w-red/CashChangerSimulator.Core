using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device.PosForDotNet.Coordination;
using CashChangerSimulator.Device.PosForDotNet.Facades;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>UPOS 規約に関連する複雑な操作（不整合フラグ管理、金種文字列解釈等）を検証するテストクラス。</summary>
[Collection("GlobalLock")]
public class UposComplianceTests
{
    private readonly Mock<IUposMediator> mediatorMock;
    private readonly Mock<CashChangerManager> managerMock;
    private readonly Inventory inventory;
    private readonly HardwareStatusManager hardwareStatusManager;
    private readonly InventoryFacade facade;

    public UposComplianceTests()
    {
        inventory = new Inventory();

        // Pre-fill inventory with some denominations so ParseCashCounts can find them
        inventory.SetCount(new DenominationKey(1000, CurrencyCashType.Bill, "JPY"), 0);
        inventory.SetCount(new DenominationKey(5000, CurrencyCashType.Bill, "JPY"), 0);

        mediatorMock = new Mock<IUposMediator>();
        mediatorMock.Setup(m => m.Execute(It.IsAny<IUposCommand>()))
            .Callback<IUposCommand>((cmd) => cmd.Execute());

        managerMock = new Mock<CashChangerManager>(
            inventory,
            new Mock<TransactionHistory>().Object,
            null,
            new ConfigurationProvider());
        hardwareStatusManager = new HardwareStatusManager();
        facade = new InventoryFacade(inventory, managerMock.Object, mediatorMock.Object);
    }

    /// <summary>AdjustCashCounts 実行時に「discrepancy」文字列が含まれる場合に不整合フラグが立つことを検証します。</summary>
    [Fact]
    public void AdjustCashCountsWithDiscrepancyStringShouldSetDiscrepancy()
    {
        // Arrange
        inventory.HasDiscrepancy = false;

        // Act
        // This method doesn't exist yet in the facade, so this will fail to compile.
        // I'll use a dynamic call or comment it out for now to show the intent,
        // but for true TDD I should add the method signature first.
        facade.AdjustCashCounts("discrepancy", "JPY", 1.0m, hardwareStatusManager);

        // Assert
        inventory.HasDiscrepancy.ShouldBeTrue();
    }

    /// <summary>AdjustCashCounts において、特定の金種カウント形式の文字列が正しく在庫に反映されることを検証します。</summary>
    [Fact]
    public void AdjustCashCountsWithCountStringShouldUpdateInventory()
    {
        // Arrange
        var currencyCode = "JPY";
        var factor = 1.0m;

        // Format: "Denom:Count,Denom:Count"
        var countsStr = "1000:5,5000:2";

        // Act
        facade.AdjustCashCounts(countsStr, currencyCode, factor, hardwareStatusManager);

        // Assert
        var key1000 = new DenominationKey(1000, CurrencyCashType.Bill, currencyCode);
        var key5000 = new DenominationKey(5000, CurrencyCashType.Bill, currencyCode);
        inventory.GetCount(key1000).ShouldBe(5);
        inventory.GetCount(key5000).ShouldBe(2);
    }

    /// <summary>ReadCashCounts 実行結果の不整合フラグが、内部在庫の状態を正しく反映していることを検証します。</summary>
    [Fact]
    public void ReadCashCountsShouldReflectDiscrepancyState()
    {
        // Arrange
        inventory.HasDiscrepancy = true;

        // Act
        var result = facade.ReadCashCounts("JPY", 1.0m);

        // Assert
        result.Discrepancy.ShouldBeTrue();

        // Clear discrepancy
        inventory.HasDiscrepancy = false;
        result = facade.ReadCashCounts("JPY", 1.0m);
        result.Discrepancy.ShouldBeFalse();
    }
}
