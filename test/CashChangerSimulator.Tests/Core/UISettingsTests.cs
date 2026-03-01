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

    /// <summary>金種の表示名（EN/JP）が正しくシリアライズおよびデシリアライズされることを検証する。</summary>
    [Fact]
    public void ConfigurationShouldSerializeAndDeserializeDenominationNames()
    {
        var config = new SimulatorConfiguration();
        var jpy = config.Inventory["JPY"];
        jpy.Denominations["B10000"] = new DenominationSettings
        {
            DisplayName = "10k Yen",
            DisplayNameJP = "一万円",
            InitialCount = 10
        };

        var toml = Toml.FromModel(config, ModelOptions);
        var loaded = Toml.ToModel<SimulatorConfiguration>(toml, options: ModelOptions);

        var loadedDenom = loaded.Inventory["JPY"].Denominations["B10000"];
        loadedDenom.DisplayName.ShouldBe("10k Yen");
        loadedDenom.DisplayNameJP.ShouldBe("一万円");
    }

    /// <summary>IsRecyclable 設定が正しくシリアライズおよびデシリアライズされることを検証する。</summary>
    [Fact]
    public void ConfigurationShouldSerializeAndDeserializeIsRecyclable()
    {
        var config = new SimulatorConfiguration();
        var jpy = config.Inventory["JPY"];
        jpy.Denominations["B2000"] = new DenominationSettings
        {
            IsRecyclable = false
        };

        var toml = Toml.FromModel(config, ModelOptions);
        var loaded = Toml.ToModel<SimulatorConfiguration>(toml, options: ModelOptions);

        loaded.Inventory["JPY"].Denominations["B2000"].IsRecyclable.ShouldBeFalse();
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

    /// <summary>ViewModel を介して金種の表示名（EN/JP）が正しくロードおよび保存されることを検証する。</summary>
    [Fact]
    public void ViewModelShouldLoadAndSaveDenominationNames()
    {
        var configProvider = new ConfigurationProvider();
        var jpy = new InventorySettings();
        jpy.Denominations["B10000"] = new DenominationSettings
        {
            DisplayName = "10k Yen",
            DisplayNameJP = "一万円",
            InitialCount = 10
        };
        configProvider.Config.Inventory["JPY"] = jpy;

        var vm = CreateViewModel(configProvider);

        var item = vm.DenominationSettings.First(i => i.Key.Value == 10000);
        item.DisplayName.Value.ShouldBe("10k Yen");
        item.DisplayNameJP.Value.ShouldBe("一万円");

        // Edit
        item.DisplayName.Value = "Ten Thousand";
        item.DisplayNameJP.Value = "壱萬円";

        // Save
        vm.SaveCommand.Execute(Unit.Default);

        var savedDenom = configProvider.Config.Inventory["JPY"].Denominations["B10000"];
        savedDenom.DisplayName.ShouldBe("Ten Thousand");
        savedDenom.DisplayNameJP.ShouldBe("壱萬円");
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
