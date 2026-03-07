using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Tests.Device;

public class SimulatorCashChangerTests
{
    [Fact]
    public void InitialState_ShouldBeClosed()
    {
        // Arrange
        var changer = new InternalSimulatorCashChanger();
        
        // Assert
        changer.State.ShouldBe(ControlState.Closed);
        changer.Claimed.ShouldBeFalse();
        changer.DeviceEnabled.ShouldBeFalse();
    }

    [Fact]
    public void Lifecycle_OpenClaimEnable_ShouldTransitionStates()
    {
        // Arrange
        var changer = new InternalSimulatorCashChanger();
        changer.SkipStateVerification = false; // Test standard behavior

        // Act & Assert: Open
        changer.Open();
        changer.State.ShouldBe(ControlState.Idle);

        // Act & Assert: Claim
        changer.Claim(1000);
        changer.Claimed.ShouldBeTrue();

        // Act & Assert: Enable
        changer.DeviceEnabled = true;
        changer.DeviceEnabled.ShouldBeTrue();

        // Act & Assert: Disable
        changer.DeviceEnabled = false;
        changer.DeviceEnabled.ShouldBeFalse();

        // Act & Assert: Release
        changer.Release();
        changer.Claimed.ShouldBeFalse();

        // Act & Assert: Close
        changer.Close();
        changer.State.ShouldBe(ControlState.Closed);
    }

    [Fact]
    public void CapProperties_ShouldReflectConfig()
    {
        // Arrange
        var changer = new InternalSimulatorCashChanger();

        // Assert
        changer.CapDeposit.ShouldBeTrue();
        changer.CapPauseDeposit.ShouldBeTrue();
        changer.CapRepayDeposit.ShouldBeTrue();
        changer.CapDiscrepancy.ShouldBeTrue();
        changer.CapFullSensor.ShouldBeTrue();
    }

    [Fact]
    public void CheckHealth_ShouldReturnOk_WhenIdle()
    {
        // Arrange
        var changer = new InternalSimulatorCashChanger();
        changer.Open();
        changer.Claim(0);
        changer.DeviceEnabled = true;

        // Act
        var result = changer.CheckHealth(HealthCheckLevel.Internal);

        // Assert
        result.ShouldContain("--- Internal Health Check Report ---");
        result.ShouldContain("Inventory: OK");
        result.ShouldContain("Status: OK");
        changer.CheckHealthText.ShouldBe(result);
    }
}
