using Microsoft.PointOfService;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>PurgeCash（回収）操作により在庫が回収庫へ移動し、不整合（Discrepancy）が発生することを検証するテストクラス。</summary>
public class PurgeCashTests : UposTestBase
{
    public PurgeCashTests()
    {
        Changer.SkipStateVerification = true;
        Changer.Open();
        Changer.Claim(0);
        Changer.DeviceEnabled = true;
    }

    /// <summary>PurgeCash 実行後にメイン在庫が空になり、不整合フラグが立つことを検証します。</summary>
    [Fact]
    public void PurgeCashShouldMoveInventoryToCollection()
    {
        // Setup initial inventory
        Changer.AdjustCashCounts(new[] { new CashCount(CashCountType.Bill, 1000, 5) });

        var initialCounts = Changer.ReadCashCounts();
        initialCounts.Counts.First(c => c.NominalValue == 1000).Count.ShouldBe(5);

        // Execute Purge
        Changer.PurgeCash();

        // Verify inventory is cleared
        var afterPurge = Changer.ReadCashCounts();
        afterPurge.Counts.First(c => c.NominalValue == 1000).Count.ShouldBe(0);

        // Verify discrepancy is true (money is in collection)
        afterPurge.Discrepancy.ShouldBeTrue("Purge moves cash to collection, which causes discrepancy.");

        Changer.ResultCode.ShouldBe((int)ErrorCode.Success);
    }
}
