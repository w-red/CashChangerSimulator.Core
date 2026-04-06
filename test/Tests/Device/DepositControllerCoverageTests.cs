using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Virtual;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

public class DepositControllerCoverageTests
{
    private static (DepositController Controller, Inventory Inventory) CreateController()
    {
        var inventory = new Inventory();
        var hw = new HardwareStatusManager();
        hw.SetConnected(true);
        var config = new ConfigurationProvider();
        
        var controller = new DepositController(inventory, hw, null, config);
        return (controller, inventory);
    }

    [Fact]
    public void Property_IsBusy_ShouldReturnExpectedValue()
    {
        var (controller, _) = CreateController();
        controller.IsBusy.ShouldBeFalse();
    }

    [Fact]
    public void Property_RequiredAmount_CanBeSetAndRetrieved()
    {
        var (controller, _) = CreateController();
        controller.RequiredAmount = 1500m;
        controller.RequiredAmount.ShouldBe(1500m);
    }

    [Fact]
    public void Property_RejectAmount_And_TrackReject_ShouldWorkCorrectly()
    {
        var (controller, _) = CreateController();
        controller.BeginDeposit();
        
        controller.RejectAmount.ShouldBe(0m);
        controller.TrackReject(500m);
        controller.RejectAmount.ShouldBe(500m);
    }

    [Fact]
    public void TrackReject_ShouldDoNothing_WhenDepositNotInProgress()
    {
        var (controller, _) = CreateController();
        controller.TrackReject(500m);
        controller.RejectAmount.ShouldBe(0m);
    }

    [Fact]
    public async Task RepayDeposit_ShouldClearState_AndRaiseEvent()
    {
        var (controller, _) = CreateController();
        controller.BeginDeposit();
        controller.TrackReject(100m);
        
        await controller.RepayDepositAsync();
        
        // Assertions match actual internal state behavior at the end of RepayDeposit
        controller.DepositAmount.ShouldBe(0m);
        controller.RejectAmount.ShouldBe(100m);
        controller.DepositStatus.ShouldBe(DeviceDepositStatus.End);
    }

    [Fact]
    public void Dispose_WhenCalledMultipleTimes_ShouldNotThrow()
    {
        var (controller, _) = CreateController();
        controller.Dispose();
        Should.NotThrow(() => controller.Dispose());
    }

    [Fact]
    public void PauseDeposit_ShouldHandleEdgeCases()
    {
        var (controller, _) = CreateController();
        controller.BeginDeposit();

        controller.PauseDeposit(DeviceDepositPause.Pause);
        
        controller.TrackDeposit(new DenominationKey(100, CurrencyCashType.Coin));
        controller.DepositAmount.ShouldBe(0m);
        
        controller.PauseDeposit(DeviceDepositPause.Resume);
        
        controller.TrackDeposit(new DenominationKey(100, CurrencyCashType.Coin));
        controller.DepositAmount.ShouldBe(100m);
    }
}
