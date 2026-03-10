using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using Shouldly;

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
        changer.CapDepositDataEvent.ShouldBeTrue();
        changer.CapPauseDeposit.ShouldBeTrue();
        changer.CapRepayDeposit.ShouldBeTrue();
        changer.CapPurgeCash.ShouldBeTrue();
        changer.CapDiscrepancy.ShouldBeTrue();
        changer.CapFullSensor.ShouldBeTrue();
        changer.CapNearFullSensor.ShouldBeTrue();
        changer.CapNearEmptySensor.ShouldBeTrue();
        changer.CapEmptySensor.ShouldBeTrue();
        changer.CapStatisticsReporting.ShouldBeTrue();
        changer.CapUpdateStatistics.ShouldBeTrue();
        changer.CapRealTimeData.ShouldBeTrue();
    }

    [Fact]
    public void Properties_ShouldReflectInternalState()
    {
        // Arrange
        var changer = new InternalSimulatorCashChanger();
        changer.Open();

        // Assert basic info
        changer.DeviceName.ShouldBe("SimulatorCashChanger");
        changer.DeviceDescription.ShouldBe("Virtual Cash Changer Simulator");

        // Currencies
        changer.CurrencyCode.ShouldBe("JPY");
        changer.CurrencyCodeList.ShouldContain("JPY");
        changer.CurrencyCodeList.ShouldContain("USD");
        changer.DepositCodeList.ShouldContain("JPY");

        // Cash Lists (Verify they don't throw)
        _ = changer.CurrencyCashList;
        _ = changer.DepositCashList;
        _ = changer.ExitCashList;

        // Deposit Info
        changer.DepositAmount.ShouldBe(0);
        changer.DepositCounts.ShouldBeEmpty();
        changer.DepositStatus.ShouldBe(CashDepositStatus.None);

        // Async result (initially success/0)
        changer.AsyncResultCode.ShouldBe(0);
    }

    [Fact]
    public void DirectIO_ShouldDelegateToHandler()
    {
        // Arrange
        var changer = new InternalSimulatorCashChanger();
        changer.Open();
        changer.Claim(0);
        changer.DeviceEnabled = true;

        // Act
        // Command 0 is not implemented in default, should return failure or empty result
        // But the goal is to see it doesn't throw and sets ResultCode
        var result = changer.DirectIO(999, 0, new object());

        // Assert
        changer.ResultCode.ShouldBe((int)ErrorCode.Success);
    }

    [Fact]
    public void Diagnostics_ShouldDelegateToFacade()
    {
        // Arrange
        var changer = new InternalSimulatorCashChanger();
        changer.Open();
        changer.Claim(0);
        changer.DeviceEnabled = true;

        // Act & Assert: CheckHealth
        var health = changer.CheckHealth(HealthCheckLevel.Internal);
        health.ShouldContain("Internal Health Check Report");
        health.ShouldContain("Status: OK");
        changer.CheckHealthText.ShouldBe(health);

        // Act & Assert: Statistics
        var stats = changer.RetrieveStatistics(["*"]);
        stats.ShouldNotBeNull();
        
        changer.UpdateStatistics([new Statistic("Test", 1)]); // Should not throw
        changer.ResetStatistics(["*"]); // Should not throw
    }

    [Fact]
    public void StatusProperties_ShouldReflectState()
    {
        var changer = new InternalSimulatorCashChanger();
        
        // When closed
        changer.DeviceStatus.ShouldBe(CashChangerStatus.OK);
        changer.FullStatus.ShouldBe(CashChangerFullStatus.OK);

        changer.Open();
        changer.Claim(0);
        changer.DeviceEnabled = true;

        // Initial idle state
        changer.DeviceStatus.ShouldBe(CashChangerStatus.OK);
        changer.FullStatus.ShouldBe(CashChangerFullStatus.OK);
    }

    [Fact]
    public void CoreOperations_ShouldNotThrowWhenEnabled()
    {
        var changer = new InternalSimulatorCashChanger();
        changer.Open();
        changer.Claim(0);
        changer.DeviceEnabled = true;

        // These should delegate and not throw in simple cases
        changer.PurgeCash();
        changer.ClearOutput();
        
        // Deposit related must be inside a session
        changer.BeginDeposit();
        changer.FixDeposit();
        changer.EndDeposit(CashDepositAction.NoChange);

        changer.BeginDeposit();
        changer.RepayDeposit();
    }

    [Fact]
    public void ResultCode_ShouldBeSettable()
    {
        var changer = new InternalSimulatorCashChanger();
        changer.ResultCode = (int)ErrorCode.Illegal;
        changer.ResultCode.ShouldBe((int)ErrorCode.Illegal);
        
        changer.ResultCodeExtended = 999;
        changer.ResultCodeExtended.ShouldBe(999);
    }

    [Fact]
    public void Exits_ShouldReflectConfig()
    {
        var changer = new InternalSimulatorCashChanger();
        changer.DeviceExits.ShouldBe(1);
        changer.CurrentExit = 1;
        changer.CurrentExit.ShouldBe(1);
    }

    [Fact]
    public void RealTimeDataEnabled_ShouldBeSettable()
    {
        var changer = new InternalSimulatorCashChanger();
        changer.RealTimeDataEnabled = true;
        changer.RealTimeDataEnabled.ShouldBeTrue();
    }
}
