using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>Test class for providing RepayDepositTests functionality.</summary>
public class RepayDepositTests
{
    private static InternalSimulatorCashChanger CreateSimulator()
    {
        // Using default constructor which resolves via SimulatorServices or creates defaults
        return new InternalSimulatorCashChanger();
    }

    /// <summary>Tests the behavior of CapRepayDepositShouldBeTrue to ensure proper functionality.</summary>
    [Fact]
    public void CapRepayDepositShouldBeTrue()
    {
        var simulator = CreateSimulator();
        simulator.CapRepayDeposit.ShouldBeTrue("UPOS standard requires CapRepayDeposit to be true to use Repay action.");
    }

    /// <summary>Tests the behavior of EndDepositWithRepayShouldNotUpdateInventory to ensure proper functionality.</summary>
    [Fact]
    public void EndDepositWithRepayShouldNotUpdateInventory()
    {
        var simulator = CreateSimulator();
        simulator.SkipStateVerification = true;
        simulator.Open();
        simulator.Claim(0);
        simulator.DeviceEnabled = true;

        var b1000 = new DenominationKey(1000, CurrencyCashType.Bill);
        
        // Initial inventory check
        var initialCounts = simulator.ReadCashCounts();
        var initialCount = initialCounts.Counts.Where(c => c.NominalValue == 1000).Select(c => c.Count).DefaultIfEmpty(0).FirstOrDefault();

        // Sequence: Begin -> Track -> Fix -> End(Repay)
        simulator.BeginDeposit();
        
        // Simulate bill insertion (Directly via controller for test simplicity, 
        // normally triggered by hardware/UI)
        var controller = (DepositController)typeof(InternalSimulatorCashChanger)
            .GetField("_depositController", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(simulator)!;
        
        controller.TrackBulkDeposit(new Dictionary<DenominationKey, int> { { b1000, 1 } });
        simulator.DepositAmount.ShouldBe(1000);

        simulator.FixDeposit();
        simulator.EndDeposit(CashDepositAction.Repay);

        // Verify state after Repay
        simulator.DepositAmount.ShouldBe(0);
        simulator.DepositStatus.ShouldBe(CashDepositStatus.End);
        
        // Verify inventory is UNCHANGED
        var finalCounts = simulator.ReadCashCounts();
        var finalCount = finalCounts.Counts.Where(c => c.NominalValue == 1000).Select(c => c.Count).DefaultIfEmpty(0).FirstOrDefault();
        finalCount.ShouldBe(initialCount, "Inventory should not be updated when action is Repay.");
    }
}
