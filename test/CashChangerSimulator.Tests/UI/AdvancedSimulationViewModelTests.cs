using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Services;
using CashChangerSimulator.UI.Wpf.ViewModels;
using Moq;
using Shouldly;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Device.Coordination;
using R3;

namespace CashChangerSimulator.Tests.UI;

public class AdvancedSimulationViewModelTests
{
    private AdvancedSimulationViewModel CreateViewModel(out HardwareStatusManager hardware)
    {
        var inv = new Inventory();
        hardware = new HardwareStatusManager();
        var controller = new DepositController(inv, hardware);
        var manager = new CashChangerManager(inv, new TransactionHistory(), new ChangeCalculator());
        var dispenseController = new DispenseController(manager, hardware, new Mock<IDeviceSimulator>().Object);
        var configProvider = new ConfigurationProvider();
        var metadataProvider = new CurrencyMetadataProvider(configProvider);
        var scriptService = new ScriptExecutionService(controller, dispenseController, inv, hardware);
        
        var cashChanger = new InternalSimulatorCashChanger(new SimulatorDependencies(
            configProvider, inv, new TransactionHistory(), manager, controller, dispenseController, 
            new OverallStatusAggregatorProvider(new MonitorsProvider(inv, configProvider, metadataProvider)), 
            hardware));

        return new AdvancedSimulationViewModel(cashChanger, scriptService, controller, metadataProvider);
    }

    [Fact]
    public void ShouldExposeHardwareErrorStates()
    {
        var vm = CreateViewModel(out var hardware);

        // Initial state
        vm.IsJammed.Value.ShouldBeFalse();
        vm.IsOverlapped.Value.ShouldBeFalse();
        vm.IsDeviceError.Value.ShouldBeFalse();

        // Jam
        hardware.SetJammed(true);
        vm.IsJammed.Value.ShouldBeTrue();

        // Overlap
        hardware.SetOverlapped(true);
        vm.IsOverlapped.Value.ShouldBeTrue();

        // Device Error
        hardware.SetDeviceError(123);
        vm.IsDeviceError.Value.ShouldBeTrue();
    }

    [Fact]
    public void ResetErrorCommandShouldClearAllErrors()
    {
        var vm = CreateViewModel(out var hardware);
        hardware.SetJammed(true);
        hardware.SetOverlapped(true);

        vm.ResetErrorCommand.Execute(Unit.Default);

        hardware.IsJammed.Value.ShouldBeFalse();
        hardware.IsOverlapped.Value.ShouldBeFalse();
    }

    [Fact]
    public void SimulateJamCommandShouldInjectJamError()
    {
        var vm = CreateViewModel(out var hardware);

        vm.SimulateJamCommand.Execute(Unit.Default);

        hardware.IsJammed.Value.ShouldBeTrue();
    }
}
