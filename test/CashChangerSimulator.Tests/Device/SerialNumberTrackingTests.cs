using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using MoneyKind4Opos.Currencies.Interfaces;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Tests.Device;

public class SerialNumberTrackingTests
{
    private (SimulatorCashChanger changer, DepositController controller) CreateChanger()
    {
        var changer = new SimulatorCashChanger();
        // SkipStateVerification allows calling BeginDeposit etc without full OPOS lifecycle
        changer.SkipStateVerification = true;
        
        // Retrieve internal controller for direct manipulation in test
        var field = typeof(SimulatorCashChanger).GetField("_depositController", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var controller = (DepositController)field!.GetValue(changer)!;
        
        return (changer, controller);
    }

    [Fact]
    public void DirectIO_GetVersion_ShouldWorkWithConstant()
    {
        // Arrange
        var (changer, _) = CreateChanger();

        // Act
        var result = changer.DirectIO(DirectIOCommands.GET_VERSION, 0, "");

        // Assert
        result.Object?.ToString().ShouldContain("SimulatorCashChanger");
    }

    [Fact]
    public void DirectIO_GetDepositedSerials_ShouldReturnEmpty_Initially()
    {
        // Arrange
        var (changer, _) = CreateChanger();

        // Act
        var result = changer.DirectIO(DirectIOCommands.GET_DEPOSITED_SERIALS, 0, "");

        // Assert
        result.Object?.ToString().ShouldBe("");
    }

    [Fact]
    public void DirectIO_GetDepositedSerials_ShouldReturnSerials_AfterDepositFix()
    {
        // Arrange
        var (changer, controller) = CreateChanger();
        var key1000 = new DenominationKey(1000, CashType.Bill, "JPY");
        
        changer.BeginDeposit();
        controller.TrackBulkDeposit(new Dictionary<DenominationKey, int> { { key1000, 2 } });
        
        // Serials are captured but not and "last" yet until FixDeposit
        controller.LastDepositedSerials.Count.ShouldBe(0);

        // Act
        changer.FixDeposit();

        // Assert
        var result = changer.DirectIO(DirectIOCommands.GET_DEPOSITED_SERIALS, 0, "");
        var serials = result.Object?.ToString()!.Split(',');
        
        serials?.Length.ShouldBe(2);
        serials?[0].ShouldStartWith("S1000-");
        serials?[1].ShouldStartWith("S1000-");
        serials?[0].ShouldNotBe(serials?[1]);
    }

    [Fact]
    public void DirectIO_GetDepositedSerials_ShouldPersistAfterEndDeposit()
    {
        // Arrange
        var (changer, controller) = CreateChanger();
        var key1000 = new DenominationKey(1000, CashType.Bill, "JPY");
        
        changer.BeginDeposit();
        controller.TrackBulkDeposit(new Dictionary<DenominationKey, int> { { key1000, 1 } });
        changer.FixDeposit();
        
        var resultBeforeHeader = changer.DirectIO(DirectIOCommands.GET_DEPOSITED_SERIALS, 0, "");
        string serialBefore = resultBeforeHeader.Object?.ToString() ?? "";

        // Act
        changer.EndDeposit(CashDepositAction.NoChange);

        // Assert
        var resultAfter = changer.DirectIO(DirectIOCommands.GET_DEPOSITED_SERIALS, 0, "");
        resultAfter.Object?.ToString().ShouldBe(serialBefore);
    }
}
