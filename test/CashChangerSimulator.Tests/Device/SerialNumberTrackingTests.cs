using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>Test class for providing SerialNumberTrackingTests functionality.</summary>
public class SerialNumberTrackingTests
{
    private static (InternalSimulatorCashChanger changer, DepositController controller) CreateChanger()
    {
        var changer = new InternalSimulatorCashChanger
        {
            // SkipStateVerification allows calling BeginDeposit etc without full OPOS lifecycle
            SkipStateVerification = true
        };
        changer.Open();
        changer.Claim(0);
        changer.Claim(0);

        // Retrieve internal controller for direct manipulation in test
        var field = typeof(InternalSimulatorCashChanger)
            .GetField(
                "_depositController",
                System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance);
        var controller =
            (DepositController)field!.GetValue(changer)!;
        
        return (changer, controller);
    }

    /// <summary>Tests the behavior of DirectIOGetVersionShouldWorkWithConstant to ensure proper functionality.</summary>
    [Fact]
    public void DirectIOGetVersionShouldWorkWithConstant()
    {
        // Arrange
        var (changer, _) = CreateChanger();

        // Act
        var result = changer.DirectIO(DirectIOCommands.GetVersion, 0, "");

        // Assert
        result.Object.ShouldNotBeNull();
        result.Object.ToString()!.ShouldContain("InternalSimulatorCashChanger");
    }

    /// <summary>Tests the behavior of DirectIOGetDepositedSerialsShouldReturnEmptyInitially to ensure proper functionality.</summary>
    [Fact]
    public void DirectIOGetDepositedSerialsShouldReturnEmptyInitially()
    {
        // Arrange
        var (changer, _) = CreateChanger();

        // Act
        var result = changer.DirectIO(DirectIOCommands.GetDepositedSerials, 0, "");

        // Assert
        result.Object.ShouldNotBeNull();
        result.Object.ToString()!.ShouldBe("");
    }

    /// <summary>Tests the behavior of DirectIOGetDepositedSerialsShouldReturnSerialsAfterDepositFix to ensure proper functionality.</summary>
    [Fact]
    public void DirectIOGetDepositedSerialsShouldReturnSerialsAfterDepositFix()
    {
        // Arrange
        var (changer, controller) = CreateChanger();
        var key1000 = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        
        changer.BeginDeposit();
        controller.TrackBulkDeposit(new Dictionary<DenominationKey, int> { { key1000, 2 } });
        
        // Serials are captured but not and "last" yet until FixDeposit
        controller.LastDepositedSerials.Count.ShouldBe(0);

        // Act
        changer.FixDeposit();

        // Assert
        var result = changer.DirectIO(DirectIOCommands.GetDepositedSerials, 0, "");
        result.Object.ShouldNotBeNull();
        var serials = result.Object.ToString()!.Split(',');
        
        serials?.Length.ShouldBe(2);
        serials?[0].ShouldStartWith("S1000-");
        serials?[1].ShouldStartWith("S1000-");
        serials?[0].ShouldNotBe(serials?[1]);
    }

    /// <summary>Tests the behavior of DirectIOGetDepositedSerialsShouldPersistAfterEndDeposit to ensure proper functionality.</summary>
    [Fact]
    public void DirectIOGetDepositedSerialsShouldPersistAfterEndDeposit()
    {
        // Arrange
        var (changer, controller) = CreateChanger();
        var key1000 = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        
        changer.BeginDeposit();
        controller.TrackBulkDeposit(new Dictionary<DenominationKey, int> { { key1000, 1 } });
        changer.FixDeposit();
        
        var resultBeforeHeader = changer.DirectIO(DirectIOCommands.GetDepositedSerials, 0, "");
        string serialBefore = resultBeforeHeader.Object?.ToString() ?? "";

        // Act
        changer.EndDeposit(CashDepositAction.NoChange);

        // Assert
        var resultAfter = changer.DirectIO(DirectIOCommands.GetDepositedSerials, 0, "");
        resultAfter.Object.ShouldNotBeNull();
        resultAfter.Object.ToString()!.ShouldBe(serialBefore);
    }
}
