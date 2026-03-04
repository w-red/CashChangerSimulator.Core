using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Services;
using Moq;
using MoneyKind4Opos.Currencies.Interfaces;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>Test class for providing BulkOperationTests functionality.</summary>
public class BulkOperationTests
{
    /// <summary>Tests the behavior of ExecuteScriptAsyncShouldUpdateInventoryCorrectly to ensure proper functionality.</summary>
    [Fact]
    public async Task ExecuteScriptAsyncShouldUpdateInventoryCorrectly()
    {
        // Arrange
        var inv = new Inventory();
        var hardware = new HardwareStatusManager();
        var controller = new DepositController(inv, hardware);
        var manager = new CashChangerManager(inv, new TransactionHistory(), new ChangeCalculator());
        var dispenseController = new DispenseController(manager, hardware, new Mock<IDeviceSimulator>().Object);
        var service = new ScriptExecutionService(controller, dispenseController, inv, hardware);

        var json = @"
        [
            { ""Op"": ""BeginDeposit"" },
            { ""Op"": ""TrackDeposit"", ""Value"": 1000, ""Count"": 5, ""Type"": ""Bill"" },
            { ""Op"": ""FixDeposit"" },
            { ""Op"": ""EndDeposit"", ""Action"": ""Store"" },
            { ""Op"": ""Dispense"", ""Value"": 2000 }
        ]";

        // Act
        hardware.SetConnected(true);
        await service.ExecuteScriptAsync(json);

        // Assert
        var key1000 = new DenominationKey(1000, CashType.Bill, "JPY");
        inv.GetCount(key1000).ShouldBe(3); // 5 deposited - 2 dispensed
    }

    /// <summary>Tests the behavior of ExecuteScriptAsyncRepayActionShouldNotUpdateInventory to ensure proper functionality.</summary>
    [Fact]
    public async Task ExecuteScriptAsyncRepayActionShouldNotUpdateInventory()
    {
        // Arrange
        var inv = new Inventory();
        var hardware = new HardwareStatusManager();
        var controller = new DepositController(inv, hardware);
        var manager = new CashChangerManager(inv, new TransactionHistory(), new ChangeCalculator());
        var dispenseController = new DispenseController(manager, hardware, new Mock<IDeviceSimulator>().Object);
        var service = new ScriptExecutionService(controller, dispenseController, inv, hardware);

        var json = @"
        [
            { ""Op"": ""BeginDeposit"" },
            { ""Op"": ""TrackDeposit"", ""Value"": 5000, ""Count"": 1 },
            { ""Op"": ""FixDeposit"" },
            { ""Op"": ""EndDeposit"", ""Action"": ""Repay"" }
        ]";

        // Act
        hardware.SetConnected(true);
        await service.ExecuteScriptAsync(json);

        // Assert
        var key5000 = new DenominationKey(5000, CashType.Bill, "JPY");
        inv.GetCount(key5000).ShouldBe(0);
    }
}