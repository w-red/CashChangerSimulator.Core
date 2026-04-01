using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Coordination;
using Microsoft.PointOfService;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>PurgeCash（回収）操作により在庫が回収庫へ移動し、不整合（Discrepancy）が発生することを検証するテストクラス。</summary>
public class PurgeCashTests
{
    private static InternalSimulatorCashChanger CreateSimulator() => new InternalSimulatorCashChanger(new SimulatorDependencies());

    /// <summary>PurgeCash 実行後にメイン在庫が空になり、不整合フラグが立つことを検証します。</summary>
    [Fact]
    public void PurgeCashShouldMoveInventoryToCollection()
    {
        var simulator = CreateSimulator();
        simulator.SkipStateVerification = true;
        simulator.Open();
        simulator.Claim(0);
        simulator.DeviceEnabled = true;

        var b1000 = new DenominationKey(1000, CurrencyCashType.Bill);
        
        // Setup initial inventory
        simulator.AdjustCashCounts(new[] { new CashCount(CashCountType.Bill, 1000, 5) });
        
        var initialCounts = simulator.ReadCashCounts();
        initialCounts.Counts.First(c => c.NominalValue == 1000).Count.ShouldBe(5);
        
        // Execute Purge
        simulator.PurgeCash();
        
        // Verify inventory is cleared
        var afterPurge = simulator.ReadCashCounts();
        afterPurge.Counts.First(c => c.NominalValue == 1000).Count.ShouldBe(0);
        
        // Verify discrepancy is true (money is in collection)
        afterPurge.Discrepancy.ShouldBeTrue("Purge moves cash to collection, which causes discrepancy.");
        
        simulator.ResultCode.ShouldBe((int)ErrorCode.Success);
    }
}
