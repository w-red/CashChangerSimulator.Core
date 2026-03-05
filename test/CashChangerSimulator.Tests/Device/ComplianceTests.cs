using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>Test class for providing ComplianceTests functionality.</summary>
public class ComplianceTests
{
    private static (InternalSimulatorCashChanger changer, DepositController controller, Inventory inventory, CashChangerSimulator.Core.Transactions.TransactionHistory history, DeviceEventHistoryObserver observer) CreateChanger()
    {
        var inventory = new Inventory();
        var hardwareStatusManager = new HardwareStatusManager();
        hardwareStatusManager.SetConnected(true);
        var history = new CashChangerSimulator.Core.Transactions.TransactionHistory();
        var controller = new DepositController(inventory, hardwareStatusManager);
        var changer =
            new InternalSimulatorCashChanger(
                null,
                inventory,
                history,
                null,
                controller,
                null,
                null,
                hardwareStatusManager);

        changer.SkipStateVerification = true;
        changer.Open();
        changer.Claim(0);
        changer.DeviceEnabled = true;
        changer.DataEventEnabled = true;

        var observer = new DeviceEventHistoryObserver(changer, history);

        return (changer, controller, inventory, history, observer);
    }

    /// <summary>Tests the behavior of ReadCashCountsShouldReportDiscrepancy to ensure proper functionality.</summary>
    [Fact]
    public void ReadCashCountsShouldReportDiscrepancy()
    {
        // Arrange
        var (changer, _, inventory, _, _) = CreateChanger();
        inventory.HasDiscrepancy = true;

        // Act
        var counts = changer.ReadCashCounts();

        // Assert
        counts.Discrepancy.ShouldBeTrue();
    }

    /// <summary>Tests the behavior of RealTimeDataEnabledFalseShouldFireDataEventOnlyOnFix to ensure proper functionality.</summary>
    [Fact]
    public void RealTimeDataEnabledFalseShouldFireDataEventOnlyOnFix()
    {
        // Arrange
        var (changer, controller, _, _, _) = CreateChanger();
        changer.RealTimeDataEnabled = false;
        int eventCount = 0;
        changer.OnEventQueued += (e) => { if (e is DataEventArgs) eventCount++; };

        // Act
        changer.BeginDeposit();
        controller.TrackDeposit(new DenominationKey(1000, CurrencyCashType.Bill, "JPY"));
        eventCount.ShouldBe(0); // Not fired yet

        changer.FixDeposit();
        eventCount.ShouldBe(2); // Fired on Fix (both internal and simulated)
    }

    /// <summary>Tests the behavior of RealTimeDataEnabledTrueShouldFireDataEventOnTrack to ensure proper functionality.</summary>
    [Fact]
    public void RealTimeDataEnabledTrueShouldFireDataEventOnTrack()
    {
        // Arrange
        var (changer, controller, _, history, _) = CreateChanger();
        changer.RealTimeDataEnabled = true;
        changer.BeginDeposit();

        int eventCount = 0;
        changer.OnEventQueued += (e) => { if (e is DataEventArgs) eventCount++; };

        // Act
        controller.TrackDeposit(new DenominationKey(1000, CurrencyCashType.Bill, "JPY"));

        // Assert
        eventCount.ShouldBe(1); // Fired immediately
        history.Entries.ShouldContain(e => e.Type == CashChangerSimulator.Core.Transactions.TransactionType.DataEvent);
    }

    /// <summary>Tests the behavior of DirectIOSimulateRemovedShouldFireStatusUpdateEvent to ensure proper functionality.</summary>
    [Fact]
    public void DirectIOSimulateRemovedShouldFireStatusUpdateEvent()
    {
        // Arrange
        var (changer, _, _, _, _) = CreateChanger();
        int status = -1;
        changer
            .OnEventQueued
            += (e) =>
            {
                if (e is StatusUpdateEventArgs se) status = se.Status;
            };

        // Act
        changer.DirectIO(DirectIOCommands.SimulateRemoved, 0, "");

        // Assert
        status.ShouldBe(41); // CHAN_STATUS_REMOVED
    }

    /// <summary>Tests the behavior of DirectIOSimulateInsertedShouldFireStatusUpdateEvent to ensure proper functionality.</summary>
    [Fact]
    public void DirectIOSimulateInsertedShouldFireStatusUpdateEvent()
    {
        // Arrange
        var (changer, _, _, _, _) = CreateChanger();
        int status = -1;
        changer.OnEventQueued += (e) => { if (e is StatusUpdateEventArgs se) status = se.Status; };

        // Act
        changer.DirectIO(DirectIOCommands.SimulateInserted, 0, "");

        // Assert
        status.ShouldBe(42); // CHAN_STATUS_INSERTED
    }

    /// <summary>Tests the behavior of DirectIOSetDiscrepancyShouldUpdateHasDiscrepancy to ensure proper functionality.</summary>
    [Fact]
    public void DirectIOSetDiscrepancyShouldUpdateHasDiscrepancy()
    {
        // Arrange
        var (changer, _, _, _, _) = CreateChanger();
        changer.ReadCashCounts().Discrepancy.ShouldBeFalse();

        // Act & Assert (Enable)
        changer.DirectIO(DirectIOCommands.SetDiscrepancy, 1, "");
        changer.ReadCashCounts().Discrepancy.ShouldBeTrue();

        // Act & Assert (Disable)
        changer.DirectIO(DirectIOCommands.SetDiscrepancy, 0, "");
        changer.ReadCashCounts().Discrepancy.ShouldBeFalse();
    }

    /// <summary>Tests the behavior of DirectIOAdjustCashCountsStrShouldUpdateInventory to ensure proper functionality.</summary>
    [Fact]
    public void DirectIOAdjustCashCountsStrShouldUpdateInventory()
    {
        // Arrange
        var (changer, _, inventory, _, _) = CreateChanger();
        var jpy1000 = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        // Seed the inventory first so the parser knows this key exists
        inventory.SetCount(jpy1000, 0);

        inventory.GetCount(jpy1000).ShouldBe(0);

        // Act
        changer.DirectIO(DirectIOCommands.AdjustCashCountsStr, 0, "1000:15");

        // Assert
        inventory.GetCount(jpy1000).ShouldBe(15);
    }
}
