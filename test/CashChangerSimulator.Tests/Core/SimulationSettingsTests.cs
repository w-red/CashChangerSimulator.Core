using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.UI.Wpf;
using CashChangerSimulator.UI.Wpf.Services;
using CashChangerSimulator.UI.Wpf.ViewModels;
using CsToml;
using R3;
using Shouldly;

namespace CashChangerSimulator.Tests.Core;

/// <summary>シミュレーション設定の保存・読み込みとバリデーションを検証するテストクラス。</summary>
public class SimulationSettingsTests
{
    [Fact]
    public void ConfigurationShouldSerializeAndDeserializeSimulationSettings()
    {
        var config = new SimulatorConfiguration();
        config.Simulation.DelayEnabled = true;
        config.Simulation.MinDelayMs = 1000;
        config.Simulation.MaxDelayMs = 3000;

        var toml = CsTomlSerializer.Serialize(config);
        var loaded = CsTomlSerializer.Deserialize<SimulatorConfiguration>(toml.ByteSpan);

        loaded.Simulation.DelayEnabled.ShouldBeTrue();
        loaded.Simulation.MinDelayMs.ShouldBe(1000);
        loaded.Simulation.MaxDelayMs.ShouldBe(3000);
    }

    [Fact]
    public void ViewModelShouldValidateDelaySettings()
    {
        var vm = CreateViewModel();

        vm.MinDelay.Value = -1;
        vm.MinDelay.HasErrors.ShouldBeTrue();

        vm.MinDelay.Value = 1000;
        vm.MaxDelay.Value = 500; // Min > Max
        vm.MaxDelay.HasErrors.ShouldBeTrue();

        vm.MaxDelay.Value = 2000;
        vm.MinDelay.HasErrors.ShouldBeFalse();
        vm.MaxDelay.HasErrors.ShouldBeFalse();
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

        vm.UseDelay.Value.ShouldBeTrue();
        vm.MinDelay.Value.ShouldBe(1234);

        // Edit
        vm.UseDelay.Value = false;
        vm.MinDelay.Value = 500;

        // Save
        vm.SaveCommand.Execute(Unit.Default);

        configProvider.Config.Simulation.DelayEnabled.ShouldBeFalse();
        configProvider.Config.Simulation.MinDelayMs.ShouldBe(500);
    }

    private static SettingsViewModel CreateViewModel(ConfigurationProvider? configProvider = null)
    {
        var cp = configProvider ?? new ConfigurationProvider();

        // Ensure JPY inventory exists for test
        if (!cp.Config.Inventory.ContainsKey("JPY"))
        {
            cp.Config.Inventory["JPY"] = new InventorySettings();
        }

        // Ensure Simulation settings exist
        if (cp.Config.Simulation == null)
        {
            cp.Config.Simulation = new SimulationSettings();
        }

        var meta = new CurrencyMetadataProvider(cp);
        var inventory = new CashChangerSimulator.Core.Models.Inventory();
        var mp = new MonitorsProvider(inventory, cp, meta);
        return new SettingsViewModel(cp, mp, meta);
    }

    [Fact]
    public void EnsureValidRange_ShouldClampDelayValues()
    {
        var settings = new SimulationSettings { MinDelayMs = -100, MaxDelayMs = -50 };
        settings.EnsureValidRange();
        settings.MinDelayMs.ShouldBe(0);
        settings.MaxDelayMs.ShouldBe(0);
    }
}
