using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device.Virtual;
using CashChangerSimulator.Device.Virtual.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>一括操作(スクリプト実行)による在庫更新や払い戻しを検証するテストクラス。</summary>
public class BulkOperationTests
{
    /// <summary>複雑な入出金スクリプトを実行し、最終的なインベントリが正しく更新されることを検証します。</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task ExecuteScriptAsyncShouldUpdateInventoryCorrectly()
    {
        // Arrange
        var inv = Inventory.Create();
        var hardware = HardwareStatusManager.Create();
        var controller = new DepositController(inv, hardware);
        var manager = new CashChangerManager(inv, new TransactionHistory(), null);
        var dispenseController = new DispenseController(manager, inv, new ConfigurationProvider(), NullLoggerFactory.Instance, hardware, new Mock<IDeviceSimulator>().Object, null);
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
        hardware.Input.IsConnected.Value = true;
        await service.ExecuteScriptAsync(json).ConfigureAwait(false);

        // Assert
        var key1000 = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        inv.GetCount(key1000).ShouldBe(3); // 5 deposited - 2 dispensed
    }

    /// <summary>Action=""Repay"" を指定した入金完了操作で、在庫が更新されない(返却される)ことを検証します。</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task ExecuteScriptAsyncRepayActionShouldNotUpdateInventory()
    {
        // Arrange
        var inv = Inventory.Create();
        var hardware = HardwareStatusManager.Create();
        var controller = new DepositController(inv, hardware);
        var manager = new CashChangerManager(inv, new TransactionHistory(), null);
        var dispenseController = new DispenseController(manager, inv, new ConfigurationProvider(), NullLoggerFactory.Instance, hardware, new Mock<IDeviceSimulator>().Object, null);
        var service = new ScriptExecutionService(controller, dispenseController, inv, hardware);

        var json = @"
        [
            { ""Op"": ""BeginDeposit"" },
            { ""Op"": ""TrackDeposit"", ""Value"": 5000, ""Count"": 1 },
            { ""Op"": ""FixDeposit"" },
            { ""Op"": ""EndDeposit"", ""Action"": ""Repay"" }
        ]";

        // Act
        hardware.Input.IsConnected.Value = true;
        await service.ExecuteScriptAsync(json).ConfigureAwait(false);

        // Assert
        var key5000 = new DenominationKey(5000, CurrencyCashType.Bill, "JPY");
        inv.GetCount(key5000).ShouldBe(0);
    }
}
