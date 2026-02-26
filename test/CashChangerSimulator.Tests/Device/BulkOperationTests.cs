using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Services;
using MoneyKind4Opos.Currencies.Interfaces;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Tests.Device;

public class BulkOperationTests
{
    [Fact]
    public async Task ExecuteScriptAsync_ShouldUpdateInventoryCorrectly()
    {
        // Arrange
        var inv = new Inventory();
        var controller = new DepositController(inv);
        var manager = new CashChangerManager(inv, new TransactionHistory(), new ChangeCalculator());
        var dispenseController = new DispenseController(manager);
        var service = new ScriptExecutionService(controller, dispenseController);

        var json = @"
        [
            { ""Op"": ""BeginDeposit"" },
            { ""Op"": ""TrackDeposit"", ""Value"": 1000, ""Count"": 5, ""Type"": ""Bill"" },
            { ""Op"": ""FixDeposit"" },
            { ""Op"": ""EndDeposit"", ""Action"": ""Store"" },
            { ""Op"": ""Dispense"", ""Value"": 2000 }
        ]";

        // Act
        await service.ExecuteScriptAsync(json);

        // Assert
        var key1000 = new DenominationKey(1000, CashType.Bill, "JPY");
        inv.GetCount(key1000).ShouldBe(3); // 5 deposited - 2 dispensed
    }

    [Fact]
    public async Task ExecuteScriptAsync_RepayAction_ShouldNotUpdateInventory()
    {
        // Arrange
        var inv = new Inventory();
        var controller = new DepositController(inv);
        var manager = new CashChangerManager(inv, new TransactionHistory(), new ChangeCalculator());
        var dispenseController = new DispenseController(manager);
        var service = new ScriptExecutionService(controller, dispenseController);

        var json = @"
        [
            { ""Op"": ""BeginDeposit"" },
            { ""Op"": ""TrackDeposit"", ""Value"": 5000, ""Count"": 1 },
            { ""Op"": ""FixDeposit"" },
            { ""Op"": ""EndDeposit"", ""Action"": ""Repay"" }
        ]";

        // Act
        await service.ExecuteScriptAsync(json);

        // Assert
        var key5000 = new DenominationKey(5000, CashType.Bill, "JPY");
        inv.GetCount(key5000).ShouldBe(0);
    }
}
