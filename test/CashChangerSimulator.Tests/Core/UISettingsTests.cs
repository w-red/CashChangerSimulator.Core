using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.UI.Wpf.ViewModels;
using R3;
using Shouldly;
using Tomlyn;

namespace CashChangerSimulator.Tests.Core;

/// <summary>UI設定の保存・読み込みを検証するテストクラス。</summary>
public class UISettingsTests
{
    private static readonly TomlModelOptions ModelOptions = new()
    {
        ConvertPropertyName = name => name
    };

    /// <summary>UIMode が正しくシリアライズおよびデシリアライズされることを検証する。</summary>
    [Fact]
    public void ConfigurationShouldSerializeAndDeserializeUIMode()
    {
        var config = new SimulatorConfiguration();
        config.System.UIMode = UIMode.PosTransaction;

        var toml = Toml.FromModel(config, ModelOptions);
        var loaded = Toml.ToModel<SimulatorConfiguration>(toml, options: ModelOptions);

        loaded.System.UIMode.ShouldBe(UIMode.PosTransaction);
    }

    /// <summary>ViewModel を介して UIMode が正しくロードおよび保存されることを検証する。</summary>
    [Fact]
    public void ViewModelShouldLoadAndSaveUIMode()
    {
        var configProvider = new ConfigurationProvider();
        configProvider.Config.System.UIMode = UIMode.PosTransaction;

        var vm = CreateViewModel(configProvider);

        vm.ActiveUIMode.Value.ShouldBe(UIMode.PosTransaction);

        // Edit
        vm.ActiveUIMode.Value = UIMode.Standard;

        // Save
        vm.SaveCommand.Execute(Unit.Default);

        configProvider.Config.System.UIMode.ShouldBe(UIMode.Standard);
    }

    private static SettingsViewModel CreateViewModel(ConfigurationProvider? configProvider = null)
    {
        var cp = configProvider ?? new ConfigurationProvider();

        // Ensure JPY inventory exists for test
        if (!cp.Config.Inventory.ContainsKey("JPY"))
        {
            cp.Config.Inventory["JPY"] = new InventorySettings();
        }

        var meta = new CurrencyMetadataProvider(cp);
        var inventory = new CashChangerSimulator.Core.Models.Inventory();
        var mp = new MonitorsProvider(inventory, cp, meta);
        return new SettingsViewModel(cp, mp, meta);
    }
}
