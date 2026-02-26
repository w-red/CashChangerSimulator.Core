using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using MoneyKind4Opos.Currencies.Interfaces;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Tests.Device;

public class ComplianceTests
{
    private (SimulatorCashChanger changer, DepositController controller, Inventory inventory) CreateChanger()
    {
        var inventory = new Inventory();
        var hardwareStatusManager = new HardwareStatusManager();
        var controller = new DepositController(inventory, hardwareStatusManager);
        var changer = new SimulatorCashChanger(null, inventory, null, null, controller, null, null, hardwareStatusManager);
        changer.SkipStateVerification = true;
        changer.DeviceEnabled = true;
        changer.DataEventEnabled = true;

        return (changer, controller, inventory);
    }

    [Fact]
    public void ReadCashCounts_ShouldReportDiscrepancy()
    {
        // Arrange
        var (changer, _, inventory) = CreateChanger();
        inventory.HasDiscrepancy = true;

        // Act
        var counts = changer.ReadCashCounts();

        // Assert
        counts.Discrepancy.ShouldBeTrue();
    }

    [Fact]
    public void RealTimeDataEnabled_False_ShouldFireDataEventOnlyOnFix()
    {
        // Arrange
        var (changer, controller, _) = CreateChanger();
        changer.RealTimeDataEnabled = false;
        int eventCount = 0;
        changer.OnEventQueued += (e) => { if (e is DataEventArgs) eventCount++; };

        // Act
        changer.BeginDeposit();
        controller.TrackDeposit(new DenominationKey(1000, CashType.Bill, "JPY"));
        eventCount.ShouldBe(0); // Not fired yet

        changer.FixDeposit();
        eventCount.ShouldBe(1); // Fired on Fix
    }

    [Fact]
    public void RealTimeDataEnabled_True_ShouldFireDataEventOnTrack()
    {
        // Arrange
        var (changer, controller, _) = CreateChanger();
        changer.RealTimeDataEnabled = true;
        changer.BeginDeposit();

        int eventCount = 0;
        changer.OnEventQueued += (e) => { if (e is DataEventArgs) eventCount++; };

        // Act
        controller.TrackDeposit(new DenominationKey(1000, CashType.Bill, "JPY"));
        
        // Assert
        eventCount.ShouldBe(1); // Fired immediately
    }

    [Fact]
    public void DirectIO_SimulateRemoved_ShouldFireStatusUpdateEvent()
    {
        // Arrange
        var (changer, _, _) = CreateChanger();
        int status = -1;
        changer.OnEventQueued += (e) => { if (e is StatusUpdateEventArgs se) status = se.Status; };

        // Act
        changer.DirectIO(DirectIOCommands.SIMULATE_REMOVED, 0, "");

        // Assert
        status.ShouldBe(41); // CHAN_STATUS_REMOVED
    }

    [Fact]
    public void DirectIO_SimulateInserted_ShouldFireStatusUpdateEvent()
    {
        // Arrange
        var (changer, _, _) = CreateChanger();
        int status = -1;
        changer.OnEventQueued += (e) => { if (e is StatusUpdateEventArgs se) status = se.Status; };

        // Act
        changer.DirectIO(DirectIOCommands.SIMULATE_INSERTED, 0, "");

        // Assert
        status.ShouldBe(42); // CHAN_STATUS_INSERTED
    }
}
