using Microsoft.PointOfService;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>在庫の調整機能(AdjustCashCounts)を検証するテストクラス。</summary>
public class AdjustCashCountsTests : UposTestBase
{
    public AdjustCashCountsTests()
    {
        Changer.SkipStateVerification = true;
        Changer.Open();
        Changer.Claim(0);
    }

    /// <summary>在庫調整後にインベントリが正しく更新されることを検証します。</summary>
    [Fact]
    public void AdjustCashCountsShouldUpdateInventory()
    {
        // Define adjustment: 10 bills of 1000 JPY, 5 coins of 100 JPY
        var cashCounts = new CashCounts(
            [
            new CashCount(CashCountType.Bill, 1000, 10),
            new CashCount(CashCountType.Coin, 100, 5)
        ], false);

        // Execute Adjustment
        Changer.AdjustCashCounts(cashCounts.Counts);

        // Verify Inventory via ReadCashCounts (which uses the internal inventory)
        var results = Changer.ReadCashCounts().Counts;

        var billCount = results.Where(c => c.NominalValue == 1000 && c.Type == CashCountType.Bill).Select(c => c.Count).DefaultIfEmpty(0).FirstOrDefault();
        var coinCount = results.Where(c => c.NominalValue == 100 && c.Type == CashCountType.Coin).Select(c => c.Count).DefaultIfEmpty(0).FirstOrDefault();

        billCount.ShouldBe(10, "1000 JPY bill count should be adjusted to 10.");
        coinCount.ShouldBe(5, "100 JPY coin count should be adjusted to 5.");
    }

    /// <summary>複数通貨がサポートされている場合に在庫調整が正しく処理されることを検証します。</summary>
    [Fact]
    public void AdjustCashCountsShouldHandleMultipleCurrenciesIfSupported()
    {
        // This test ensures that the adjustment logic correctly identifies the active currency
        // and updates the inventory accordingly.
        // JPY is default
        var cashCounts = new CashCounts(
        [
            new CashCount(CashCountType.Bill, 5000, 3)
        ], false);

        Changer.AdjustCashCounts(cashCounts.Counts);

        var results = Changer.ReadCashCounts().Counts;
        var count = results.Where(c => c.NominalValue == 5000).Select(c => c.Count).DefaultIfEmpty(0).FirstOrDefault();
        count.ShouldBe(3);
    }
}
