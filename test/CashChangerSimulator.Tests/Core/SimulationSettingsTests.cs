namespace CashChangerSimulator.Tests.Core;

using System;
using System.Collections.Generic;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.UI.Wpf.ViewModels;
using CashChangerSimulator.UI.Wpf;
using CashChangerSimulator.UI.Wpf.Services;
using Shouldly;
using Xunit;
using CsToml;

public class SimulationSettingsTests
{
    [Fact]
    public void ConfigurationShouldSerializeAndDeserializeSimulationSettings()
    {
        var config = new SimulatorConfiguration();
        config.Simulation.DelayEnabled = true;
        config.Simulation.MinDelayMs = 1000;
        config.Simulation.MaxDelayMs = 3000;
        config.Simulation.RandomErrorsEnabled = true;
        config.Simulation.ErrorRate = 5; // 5%

        var toml = CsTomlSerializer.Serialize(config);
        var loaded = CsTomlSerializer.Deserialize<SimulatorConfiguration>(toml.ByteSpan);

        loaded.Simulation.DelayEnabled.ShouldBeTrue();
        loaded.Simulation.MinDelayMs.ShouldBe(1000);
        loaded.Simulation.MaxDelayMs.ShouldBe(3000);
        loaded.Simulation.RandomErrorsEnabled.ShouldBeTrue();
        loaded.Simulation.ErrorRate.ShouldBe(5);
    }

    [Fact]
    public void ViewModelShouldValidateDelaySettings()
    {
        var vm = CreateViewModel();
        
        vm.MinDelay = -1;
        vm.HasErrors.ShouldBeTrue();
        vm.GetErrors(nameof(vm.MinDelay)).Cast<object>().ShouldNotBeEmpty();

        vm.MinDelay = 1000;
        vm.MaxDelay = 500; // Min > Max
        vm.HasErrors.ShouldBeTrue();
        vm.GetErrors(nameof(vm.MaxDelay)).Cast<object>().ShouldNotBeEmpty();

        vm.MaxDelay = 2000;
        vm.HasErrors.ShouldBeFalse();
    }

    [Fact]
    public void ViewModelShouldValidateErrorRate()
    {
        var vm = CreateViewModel();

        vm.ErrorRate = -1;
        vm.HasErrors.ShouldBeTrue();

        vm.ErrorRate = 101;
        vm.HasErrors.ShouldBeTrue();

        vm.ErrorRate = 50;
        vm.HasErrors.ShouldBeFalse();
    }

    [Fact]
    public void ViewModelShouldLoadAndSaveSimulationSettings()
    {
        var configProvider = new ConfigurationProvider();
        if (configProvider.Config.Simulation == null)
        {
            configProvider.Config.Simulation = new SimulationSettings();
        }
        configProvider.Config.Simulation.DelayEnabled = true;
        configProvider.Config.Simulation.MinDelayMs = 1234;
        
        var vm = CreateViewModel(configProvider);
        
        vm.UseDelay.ShouldBeTrue();
        vm.MinDelay.ShouldBe(1234);

        // Edit
        vm.UseDelay = false;
        vm.MinDelay = 500;
        
        // Save
        vm.SaveCommand.Execute(null);

        configProvider.Config.Simulation.DelayEnabled.ShouldBeFalse();
        configProvider.Config.Simulation.MinDelayMs.ShouldBe(500);
    }

    private SettingsViewModel CreateViewModel(ConfigurationProvider? configProvider = null)
    {
        var cp = configProvider ?? new ConfigurationProvider();
        
        // Ensure JPY inventory exists for test
        if (!cp.Config.MultiInventory.ContainsKey("JPY"))
        {
            cp.Config.MultiInventory["JPY"] = new InventorySettings();
        }

        // Ensure Simulation settings exist
        if (cp.Config.Simulation == null)
        {
            cp.Config.Simulation = new SimulationSettings();
        }

        var meta = new CashChangerSimulator.UI.Wpf.Services.CurrencyMetadataProvider(cp);
        var inventory = new CashChangerSimulator.Core.Models.Inventory();
        var mp = new MonitorsProvider(inventory, cp, meta);
        return new SettingsViewModel(cp, mp, meta);
    }
}
