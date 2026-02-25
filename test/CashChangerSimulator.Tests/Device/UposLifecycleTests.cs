using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using MoneyKind4Opos.Currencies.Interfaces;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>SimulatorCashChanger の UPOS ライフサイクル（Open/Claim/Release/Close）を検証するテストクラス (TDD)。</summary>
public class UposLifecycleTests
{
    private static SimulatorCashChanger CreateCashChanger()
    {
        var configProvider = new ConfigurationProvider();
        configProvider.Config.Inventory["JPY"] = new InventorySettings
        {
            Denominations = new()
            {
                ["C100"] = new() { InitialCount = 50 }
            }
        };

        var inv = new Inventory();
        inv.SetCount(new DenominationKey(100, CashType.Coin, "JPY"), 50);

        var hw = new HardwareStatusManager();
        var history = new TransactionHistory();
        var manager = new CashChangerManager(inv, history, new ChangeCalculator());
        var metadataProvider = new CurrencyMetadataProvider(configProvider);
        var monitorsProvider = new MonitorsProvider(inv, configProvider, metadataProvider);
        var aggregatorProvider = new OverallStatusAggregatorProvider(monitorsProvider);
        var depositController = new DepositController(inv);
        var dispenseController = new DispenseController(manager, null, new Mock<IDeviceSimulator>().Object);

        return new SimulatorCashChanger(configProvider, inv, history, manager, depositController, dispenseController, aggregatorProvider, hw);
    }

    /// <summary>Claim されていない状態で DispenseChange を呼び出すと例外がスローされることを検証する。</summary>
    [Fact]
    public void DispenseChangeShouldThrowWhenNotClaimed()
    {
        var cc = CreateCashChanger();

        // Device is not Open/Claimed — DispenseChange should throw
        var ex = Should.Throw<PosControlException>(() => cc.DispenseChange(100));
        // BasicServiceObject should enforce Closed/NotClaimed
        (ex.ErrorCode == ErrorCode.Closed || ex.ErrorCode == ErrorCode.NotClaimed)
            .ShouldBeTrue($"Expected Closed or NotClaimed, but got {ex.ErrorCode}");
    }

    /// <summary>Claim されていない状態で BeginDeposit を呼び出すと例外がスローされることを検証する。</summary>
    [Fact]
    public void BeginDepositShouldThrowWhenNotClaimed()
    {
        var cc = CreateCashChanger();

        var ex = Should.Throw<PosControlException>(() => cc.BeginDeposit());
        ex.ErrorCode.ShouldBe(ErrorCode.Closed);
    }

    /// <summary>Claim されていない状態で ReadCashCounts を呼び出すと例外がスローされることを検証する。</summary>
    [Fact]
    public void ReadCashCountsShouldThrowWhenNotClaimed()
    {
        var cc = CreateCashChanger();

        var ex = Should.Throw<PosControlException>(() => cc.ReadCashCounts());
        ex.ErrorCode.ShouldBe(ErrorCode.Closed);
    }

    /// <summary>CheckHealth が常に OK を返すことを検証する。</summary>
    [Fact]
    public void CheckHealthShouldReturnOk()
    {
        var cc = CreateCashChanger();

        // CheckHealth should work regardless of state
        cc.CheckHealth(HealthCheckLevel.Internal).ShouldBe("OK");
    }
}
