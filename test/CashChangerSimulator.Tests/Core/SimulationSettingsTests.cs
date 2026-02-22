using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.UI.Wpf;
using CashChangerSimulator.UI.Wpf.Services;
using CashChangerSimulator.UI.Wpf.ViewModels;
using CsToml;
using R3;
using Shouldly;

namespace CashChangerSimulator.Tests.Core;

/// <summary>シミュレーション設定の保存・読み込みを検証するテストクラス。</summary>
public class SimulationSettingsTests
{
    [Fact]
    public void ConfigurationShouldSerializeAndDeserializeSimulationSettings()
    {
        var config = new SimulatorConfiguration();
        config.Simulation.UIMode = UIMode.PosTransaction;

        var toml = CsTomlSerializer.Serialize(config);
        var loaded = CsTomlSerializer.Deserialize<SimulatorConfiguration>(toml.ByteSpan);

        loaded.Simulation.UIMode.ShouldBe(UIMode.PosTransaction);
    }

    [Fact]
    public void ViewModelShouldLoadAndSaveSettings()
    {
        var configProvider = new ConfigurationProvider();
        if (configProvider.Config.Simulation == null)
        {
            configProvider.Config.Simulation = new SimulationSettings();
        }
        configProvider.Config.Simulation.UIMode = UIMode.PosTransaction;

        var vm = CreateViewModel(configProvider);

        vm.ActiveUIMode.Value.ShouldBe(UIMode.PosTransaction);

        // Edit
        vm.ActiveUIMode.Value = UIMode.Standard;

        // Save
        vm.SaveCommand.Execute(Unit.Default);

        configProvider.Config.Simulation.UIMode.ShouldBe(UIMode.Standard);
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
}
