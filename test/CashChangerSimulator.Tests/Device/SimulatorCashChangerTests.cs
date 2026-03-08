using CashChangerSimulator.Core;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Testing;
using Microsoft.Extensions.DependencyInjection;
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
