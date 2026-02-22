using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using MoneyKind4Opos.Currencies.Interfaces;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>SimulatorCashChanger の UPOS ライフサイクル（Open/Claim/Release/Close）を検証するテストクラス (TDD)。</summary>
public class UposLifecycleTests
{
    private static SimulatorCashChanger CreateCashChanger()
    {
        var config = new SimulatorConfiguration();
        config.Inventory["JPY"] = new InventorySettings
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
        var manager = new CashChangerManager(inv, history);

        return new SimulatorCashChanger(config, inv, history, manager, null, null, null, hw);
    }

    [Fact]
    public void DispenseChange_ShouldThrow_WhenNotClaimed()
    {
        var cc = CreateCashChanger();

        // Device is not Open/Claimed — DispenseChange should throw
        var ex = Should.Throw<PosControlException>(() => cc.DispenseChange(100));
        // BasicServiceObject should enforce Closed/NotClaimed
        (ex.ErrorCode == ErrorCode.Closed || ex.ErrorCode == ErrorCode.NotClaimed)
            .ShouldBeTrue($"Expected Closed or NotClaimed, but got {ex.ErrorCode}");
    }

    [Fact]
    public void BeginDeposit_ShouldThrow_WhenNotClaimed()
    {
        var cc = CreateCashChanger();

        var ex = Should.Throw<PosControlException>(() => cc.BeginDeposit());
        ex.ErrorCode.ShouldBe(ErrorCode.Closed);
    }

    [Fact]
    public void ReadCashCounts_ShouldThrow_WhenNotClaimed()
    {
        var cc = CreateCashChanger();

        var ex = Should.Throw<PosControlException>(() => cc.ReadCashCounts());
        ex.ErrorCode.ShouldBe(ErrorCode.Closed);
    }

    [Fact]
    public void CheckHealth_ShouldReturnOk()
    {
        var cc = CreateCashChanger();

        // CheckHealth should work regardless of state
        cc.CheckHealth(HealthCheckLevel.Internal).ShouldBe("OK");
    }
}
