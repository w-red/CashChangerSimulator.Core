using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Coordination;
using CashChangerSimulator.Device.Facades;
using Microsoft.PointOfService;
using Moq;
using Shouldly;
using Xunit;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Configuration;

namespace CashChangerSimulator.Tests.Device;

public class UposComplianceTests
{
    private readonly Mock<IUposMediator> _mediatorMock;
    private readonly Mock<CashChangerManager> _managerMock;
    private readonly Inventory Inventory;
    private readonly HardwareStatusManager HardwareStatusManager;
    private readonly InventoryFacade _facade;

    public UposComplianceTests()
    {
        Inventory = new Inventory();
        // Pre-fill inventory with some denominations so ParseCashCounts can find them
        Inventory.SetCount(new DenominationKey(1000, CurrencyCashType.Bill, "JPY"), 0);
        Inventory.SetCount(new DenominationKey(5000, CurrencyCashType.Bill, "JPY"), 0);

        _mediatorMock = new Mock<IUposMediator>();
        _mediatorMock.Setup(m => m.Execute(It.IsAny<IUposCommand>()))
            .Callback<IUposCommand>((cmd) => cmd.Execute());

        _managerMock = new Mock<CashChangerManager>(
            Inventory, 
            new Mock<TransactionHistory>().Object, 
            new ChangeCalculator(), 
            new ConfigurationProvider());
        HardwareStatusManager = new HardwareStatusManager();
        _facade = new InventoryFacade(Inventory, _managerMock.Object, _mediatorMock.Object);
    }

    [Fact]
    public void AdjustCashCounts_WithDiscrepancyString_ShouldSetDiscrepancy()
    {
        // Arrange
        Inventory.HasDiscrepancy = false;

        // Act
        // This method doesn't exist yet in the facade, so this will fail to compile.
        // I'll use a dynamic call or comment it out for now to show the intent, 
        // but for true TDD I should add the method signature first.
        _facade.AdjustCashCounts("discrepancy", "JPY", 1.0m, HardwareStatusManager);

        // Assert
        Inventory.HasDiscrepancy.ShouldBeTrue();
    }

    [Fact]
    public void AdjustCashCounts_WithCountString_ShouldUpdateInventory()
    {
        // Arrange
        var currencyCode = "JPY";
        var factor = 1.0m;
        // Format: "Denom:Count,Denom:Count"
        var countsStr = "1000:5,5000:2"; 

        // Act
        _facade.AdjustCashCounts(countsStr, currencyCode, factor, HardwareStatusManager);

        // Assert
        var key1000 = new DenominationKey(1000, CurrencyCashType.Bill, currencyCode);
        var key5000 = new DenominationKey(5000, CurrencyCashType.Bill, currencyCode);
        Inventory.GetCount(key1000).ShouldBe(5);
        Inventory.GetCount(key5000).ShouldBe(2);
    }

    [Fact]
    public void ReadCashCounts_ShouldReflectDiscrepancyState()
    {
        // Arrange
        Inventory.HasDiscrepancy = true;

        // Act
        var result = _facade.ReadCashCounts("JPY", 1.0m);
 
        // Assert
        result.Discrepancy.ShouldBeTrue();
 
        // Clear discrepancy
        Inventory.HasDiscrepancy = false;
        result = _facade.ReadCashCounts("JPY", 1.0m);
        result.Discrepancy.ShouldBeFalse();
    }
}
