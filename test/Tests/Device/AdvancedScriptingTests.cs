using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Services;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>高度なスクリプト機能（ループ、変数）のテストクラス。</summary>
public class AdvancedScriptingTests
{
    /// <summary>スクリプト内の Repeat（ループ）操作が期待通りに複数回実行されることを検証します。</summary>
    [Fact]
    public async Task ExecuteScriptAsyncRepeatShouldExecuteMultipleTimes()
    {
        // Arrange
        var inv = new Inventory();
        var hardware = new HardwareStatusManager();
        hardware.SetConnected(true);
        var controller = new DepositController(inv, hardware);
        var manager = new CashChangerManager(inv, new TransactionHistory(), new ChangeCalculator());
        var dispenseController = new DispenseController(manager, hardware, new Mock<IDeviceSimulator>().Object);
        var service = new ScriptExecutionService(controller, dispenseController, inv, hardware);

        // 1000円を3回投入するループ
        var json = @"
        [
            { 
                ""Op"": ""Repeat"", 
                ""Count"": 3, 
                ""Commands"": [
                    { ""Op"": ""BeginDeposit"" },
                    { ""Op"": ""TrackDeposit"", ""Value"": 1000, ""Count"": 1 },
                    { ""Op"": ""FixDeposit"" },
                    { ""Op"": ""EndDeposit"", ""Action"": ""Store"" }
                ]
            }
        ]";

        // Act
        await service.ExecuteScriptAsync(json);

        // Assert
        var key1000 = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        inv.GetCount(key1000).ShouldBe(3);
    }

    /// <summary>スクリプト内で変数をセットし、動的なパラメータとして後続のコマンドで使用できることを検証します。</summary>
    [Fact]
    public async Task ExecuteScriptAsyncSetVariableShouldAllowDynamicParameters()
    {
        // Arrange
        var inv = new Inventory();
        var hardware = new HardwareStatusManager();
        hardware.SetConnected(true);
        var controller = new DepositController(inv, hardware);
        var manager = new CashChangerManager(inv, new TransactionHistory(), new ChangeCalculator());
        var dispenseController = new DispenseController(manager, hardware, new Mock<IDeviceSimulator>().Object);
        var service = new ScriptExecutionService(controller, dispenseController, inv, hardware);

        // 変数 'amount' をセットし、それを Dispense で使用する
        // Note: 実装前なので形式は仮定
        var json = @"
        [
            { ""Op"": ""Set"", ""Variable"": ""amount"", ""Value"": 2000 },
            { ""Op"": ""BeginDeposit"" },
            { ""Op"": ""TrackDeposit"", ""Value"": 1000, ""Count"": 3 },
            { ""Op"": ""FixDeposit"" },
            { ""Op"": ""EndDeposit"", ""Action"": ""Store"" },
            { ""Op"": ""Dispense"", ""Value"": ""$amount"" }
        ]";

        // Act
        await service.ExecuteScriptAsync(json);

        // Assert
        var key1000 = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        inv.GetCount(key1000).ShouldBe(1); // 3 deposited - 2 dispensed
        inv.CalculateTotal().ShouldBe(1000); // 3000 - 2000
    }

    /// <summary>スクリプト経由でハードウェアエラーを注入し、デバイス状態が正しく更新されることを検証します。</summary>
    [Fact]
    public async Task ExecuteScriptAsyncInjectErrorShouldChangeHardwareState()
    {
        // Arrange
        var inv = new Inventory();
        var hardware = new HardwareStatusManager();
        hardware.SetConnected(true);
        var controller = new DepositController(inv, hardware);
        var manager = new CashChangerManager(inv, new TransactionHistory(), new ChangeCalculator());
        var dispenseController = new DispenseController(manager, hardware, new Mock<IDeviceSimulator>().Object);
        var service = new ScriptExecutionService(controller, dispenseController, inv, hardware);

        // ジャムを注入し、その後の操作が失敗することを確認する
        var json = @"
        [
            { ""Op"": ""Inject-Error"", ""Error"": ""Jam"" },
            { ""Op"": ""BeginDeposit"" }
        ]";

        // Act & Assert
        // BeginDeposit はハードウェアエラー状態（Jammed）なので PosControlException を投げるべき
        await Should.ThrowAsync<Microsoft.PointOfService.PosControlException>(async () => await service.ExecuteScriptAsync(json));
        hardware.IsJammed.Value.ShouldBeTrue();
    }

    /// <summary>スクリプト内の Assert 操作により、現在のインベントリ状態が正しく検証されることを確認します。</summary>
    [Fact]
    public async Task ExecuteScriptAsyncAssertShouldVerifyInventory()
    {
        // Arrange
        var inv = new Inventory();
        var hardware = new HardwareStatusManager();
        hardware.SetConnected(true);
        var controller = new DepositController(inv, hardware);
        var manager = new CashChangerManager(inv, new TransactionHistory(), new ChangeCalculator());
        var dispenseController = new DispenseController(manager, hardware, new Mock<IDeviceSimulator>().Object);
        var service = new ScriptExecutionService(controller, dispenseController, inv, hardware);

        // 在庫の枚数をアサーションする
        var json = @"
        [
            { ""Op"": ""BeginDeposit"" },
            { ""Op"": ""TrackDeposit"", ""Value"": 500, ""Count"": 2, ""Type"": ""Coin"" },
            { ""Op"": ""FixDeposit"" },
            { ""Op"": ""EndDeposit"", ""Action"": ""Store"" },
            { ""Op"": ""Assert"", ""Target"": ""Inventory"", ""Denom"": 500, ""Value"": 2, ""Type"": ""Coin"" }
        ]";

        // Act
        await service.ExecuteScriptAsync(json);

        // Assert (スクリプト内で Assert が通れば例外は出ない)
        var key500 = new DenominationKey(500, CurrencyCashType.Coin, "JPY");
        inv.GetCount(key500).ShouldBe(2);
    }

    /// <summary>特定の箇所（Inletなど）へのジャム注入がハードウェア状態に正しく反映されることを検証します。</summary>
    [Fact]
    public async Task ExecuteScriptAsyncInjectErrorJamLocationShouldUpdateHardware()
    {
        // Arrange
        var inv = new Inventory();
        var hardware = new HardwareStatusManager();
        hardware.SetConnected(true);
        var controller = new DepositController(inv, hardware);
        var manager = new CashChangerManager(inv, new TransactionHistory(), new ChangeCalculator());
        var dispenseController = new DispenseController(manager, hardware, new Mock<IDeviceSimulator>().Object);
        var service = new ScriptExecutionService(controller, dispenseController, inv, hardware);

        // 特定の箇所でのジャムを注入
        var json = @"
        [
            { ""Op"": ""Inject-Error"", ""Error"": ""Jam"", ""Location"": ""Inlet"" }
        ]";

        // Act
        await service.ExecuteScriptAsync(json);

        // Assert
        hardware.IsJammed.Value.ShouldBeTrue();
        hardware.JamLocation.Value.ShouldBe(JamLocation.Inlet);
    }

    /// <summary>汎用デバイスエラーの注入がハードウェア状態およびエラーコードに正しく反映されることを検証します。</summary>
    [Fact]
    public async Task ExecuteScriptAsyncInjectErrorDeviceShouldUpdateHardware()
    {
        // Arrange
        var inv = new Inventory();
        var hardware = new HardwareStatusManager();
        hardware.SetConnected(true);
        var controller = new DepositController(inv, hardware);
        var manager = new CashChangerManager(inv, new TransactionHistory(), new ChangeCalculator());
        var dispenseController = new DispenseController(manager, hardware, new Mock<IDeviceSimulator>().Object);
        var service = new ScriptExecutionService(controller, dispenseController, inv, hardware);

        // 汎用デバイスエラーを注入
        var json = @"
        [
            { ""Op"": ""Inject-Error"", ""Error"": ""Device"", ""ErrorCode"": 111, ""ErrorCodeExtended"": 222 }
        ]";

        // Act
        await service.ExecuteScriptAsync(json);

        // Assert
        hardware.IsDeviceError.Value.ShouldBeTrue();
        hardware.CurrentErrorCode.Value.ShouldBe(111);
        hardware.CurrentErrorCodeExtended.Value.ShouldBe(222);
    }
}
