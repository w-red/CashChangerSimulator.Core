using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Coordination;
using CashChangerSimulator.UI.Wpf.Services;
using CashChangerSimulator.UI.Wpf.ViewModels;
using CashChangerSimulator.Tests.Mocks;
using Moq;
using R3;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Tests.UI;

public class DispenseViewModelTests
{
    private static (DispenseViewModel vm, IDeviceFacade facade, ConfigurationProvider config) CreateViewModel()
    {
        var config = new ConfigurationProvider();
        config.Config.System.CurrencyCode = TestConstants.DefaultCurrency;
        
        var inv = new Inventory();
        inv.SetCount(TestConstants.Key100, 100);
        inv.SetCount(TestConstants.Key1000, 100);

        var history = new TransactionHistory();
        var manager = new CashChangerManager(inv, history, new ChangeCalculator(), config);
        var metadataProvider = new CurrencyMetadataProvider(config);
        var monitorsProvider = new MonitorsProvider(inv, config, metadataProvider);
        var aggregatorProvider = new OverallStatusAggregatorProvider(monitorsProvider);
        var hw = new HardwareStatusManager();
        var sideSimulator = new Mock<IDeviceSimulator>();
        var dispenseController = new DispenseController(manager, hw, sideSimulator.Object);
        var depositController = new DepositController(inv, hw);
        
        var mockChanger = new Mock<InternalSimulatorCashChanger>(new SimulatorDependencies(
            config, inv, history, manager, depositController, dispenseController, aggregatorProvider, hw));

        var notifyService = new Mock<INotifyService>().Object;
        var viewService = new ImmediateViewService();
        var dispatcher = new ImmediateDispatcherService();

        var facade = new DeviceFacade(
            inv,
            manager,
            depositController,
            dispenseController,
            hw,
            mockChanger.Object,
            history,
            aggregatorProvider,
            monitorsProvider,
            notifyService,
            dispatcher,
            viewService);

        var isInDepositMode = new BindableReactiveProperty<bool>(false);
        var vm = new DispenseViewModel(
            facade,
            config,
            isInDepositMode,
            () => new List<DenominationViewModel>(),
            notifyService,
            metadataProvider);

        return (vm, facade, config);
    }

    [Fact]
    public void TotalAmountShouldReflectInventoryTotal()
    {
        // Arrange
        var (vm, _, _) = CreateViewModel();

        // Assert
        vm.TotalAmount.Value.ShouldBe(100 * 100 + 1000 * 100);
    }

    [Fact]
    public void DispenseCommandShouldBeDisabledWhenAmountIsInvalid()
    {
        // Arrange
        var (vm, _, _) = CreateViewModel();

        // Act & Assert
        vm.DispenseAmountInput.Value = "";
        ((ICommand)vm.DispenseCommand).CanExecute(null).ShouldBeFalse();

        vm.DispenseAmountInput.Value = "abc";
        ((ICommand)vm.DispenseCommand).CanExecute(null).ShouldBeFalse();

        vm.DispenseAmountInput.Value = "-100";
        ((ICommand)vm.DispenseCommand).CanExecute(null).ShouldBeFalse();

        vm.DispenseAmountInput.Value = "0";
        ((ICommand)vm.DispenseCommand).CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public void DispenseCommandShouldBeDisabledWhenInsufficientFunds()
    {
        // Arrange
        var (vm, _, _) = CreateViewModel();
        var tooMuch = (100 * 100 + 1000 * 100) + 1;

        // Act
        vm.DispenseAmountInput.Value = tooMuch.ToString();

        // Assert
        ((ICommand)vm.DispenseCommand).CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public void DispenseCommandShouldExecuteDispense()
    {
        // Arrange
        var (vm, facade, _) = CreateViewModel();
        vm.DispenseAmountInput.Value = "1100";

        // Act
        vm.DispenseCommand.Execute(Unit.Default);

        // Assert
        vm.DispensingAmount.Value.ShouldBe(1100);
        // In ImmediateDispatcherService, DispenseChangeAsync is called.
        // We can't easily verify the async call result without more mocks, 
        // but we verified the command execution flow.
    }

    [Fact]
    public void QuickDispenseCommandShouldExecuteDispense()
    {
        // Arrange
        var (vm, facade, _) = CreateViewModel();
        var metadata = new CurrencyMetadataProvider(new ConfigurationProvider());
        var monitor = facade.Monitors.Monitors.First(m => m.Key == TestConstants.Key100);
        var config = new ConfigurationProvider();
        var denominationVm = new DenominationViewModel(facade, TestConstants.Key100, metadata, monitor, config);

        // Act
        vm.QuickDispenseCommand.Execute(denominationVm);

        // Assert
        vm.DispensingAmount.Value.ShouldBe(100);
    }

    [Fact]
    public void DispenseCommandShouldBeDisabledDuringDeposit()
    {
        // Arrange
        var config = new ConfigurationProvider();
        var inv = new Inventory();
        var hw = new HardwareStatusManager();
        var manager = new CashChangerManager(inv, new TransactionHistory(), new ChangeCalculator());
        var dispenseController = new DispenseController(manager, hw, new Mock<IDeviceSimulator>().Object);
        var metadata = new CurrencyMetadataProvider(config);
        var monitors = new MonitorsProvider(inv, config, metadata);
        var mockChanger = new Mock<SimulatorCashChanger>(new SimulatorDependencies(config, inv, new TransactionHistory(), manager, new DepositController(inv, hw), dispenseController, new OverallStatusAggregatorProvider(monitors), hw));
        var facade = new DeviceFacade(inv, manager, new DepositController(inv, hw), dispenseController, hw, mockChanger.Object, new TransactionHistory(), new OverallStatusAggregatorProvider(monitors), monitors, new Mock<INotifyService>().Object, new ImmediateDispatcherService(), new ImmediateViewService());
        
        var isInDepositMode = new BindableReactiveProperty<bool>(true);
        var vm = new DispenseViewModel(facade, config, isInDepositMode, () => [], new Mock<INotifyService>().Object, metadata);

        vm.DispenseAmountInput.Value = "100";

        // Assert
        ((ICommand)vm.DispenseCommand).CanExecute(null).ShouldBeFalse();
    }
}
