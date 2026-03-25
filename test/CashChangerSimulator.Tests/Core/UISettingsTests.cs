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
    private static readonly TomlSerializerOptions ModelOptions = new()
    {
        PropertyNamingPolicy = null
    };

    /// <summary>UIMode が正しくシリアライズおよびデシリアライズされることを検証する。</summary>
    [Fact]
    public void ConfigurationShouldSerializeAndDeserializeUIMode()
    {
        var config = new SimulatorConfiguration();
        config.System.UIMode = UIMode.Standard;

        var toml = TomlSerializer.Serialize(config, ModelOptions);
        var loaded = TomlSerializer.Deserialize<SimulatorConfiguration>(toml, options: ModelOptions) ?? new SimulatorConfiguration();

        loaded.System.UIMode.ShouldBe(UIMode.Standard);
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

        var toml = TomlSerializer.Serialize(config, ModelOptions);
        var loaded = TomlSerializer.Deserialize<SimulatorConfiguration>(toml, options: ModelOptions) ?? new SimulatorConfiguration();

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

        var toml = TomlSerializer.Serialize(config, ModelOptions);
        var loaded = TomlSerializer.Deserialize<SimulatorConfiguration>(toml, options: ModelOptions) ?? new SimulatorConfiguration();

        loaded.Inventory["JPY"].Denominations["B2000"].IsRecyclable.ShouldBeFalse();
    }

    /// <summary>基本テーマ（BaseTheme）が正しくシリアライズおよびデシリアライズされることを検証します。</summary>
    [Fact]
    public void ConfigurationShouldSerializeAndDeserializeBaseTheme()
    {
        var config = new SimulatorConfiguration();
        config.System.BaseTheme = "Light";

        var toml = TomlSerializer.Serialize(config, ModelOptions);
        var loaded = TomlSerializer.Deserialize<SimulatorConfiguration>(toml, options: ModelOptions) ?? new SimulatorConfiguration();

        loaded.System.BaseTheme.ShouldBe("Light");
    }

    /// <summary>在庫しきい値（Thresholds）が正しくシリアライズおよびデシリアライズされることを検証します。</summary>
    [Fact]
    public void ConfigurationShouldSerializeAndDeserializeThresholds()
    {
        var config = new SimulatorConfiguration();
        config.Thresholds.NearEmpty = 5;
        config.Thresholds.Full = 200;

        var toml = TomlSerializer.Serialize(config, ModelOptions);
        var loaded = TomlSerializer.Deserialize<SimulatorConfiguration>(toml, options: ModelOptions) ?? new SimulatorConfiguration();

        loaded.Thresholds.NearEmpty.ShouldBe(5);
        loaded.Thresholds.Full.ShouldBe(200);
    }

    /// <summary>ViewModel を介して UIMode が正しくロードおよび保存されることを検証する。</summary>
    [Fact]
    public void ViewModelShouldLoadAndSaveUIMode()
    {
        var configProvider = new ConfigurationProvider();
        configProvider.Config.System.UIMode = UIMode.Standard;

        var vm = CreateViewModel(configProvider);

        vm.ActiveUIMode.Value.ShouldBe(UIMode.Standard);

        // Edit (Remaining one mode)
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

    /// <summary>通貨コードやカルチャの変更に伴い、通貨記号や単位が正しく更新されることを検証します。</summary>
    [Fact]
    public void CurrencyMetadataProviderShouldUpdateSymbolsWhenConfigReloaded()
    {
        // Arrange
        var config = new SimulatorConfiguration();
        config.System.CurrencyCode = "JPY";
        config.System.CultureCode = "en-US";

        var tempFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Guid.NewGuid().ToString() + ".toml");
        ConfigurationLoader.Save(config, tempFile);

        var provider = ConfigurationProvider.CreateWithFilePath(tempFile);
        var metadata = new CurrencyMetadataProvider(provider);

        // Initial state (JPY, en-US) -> Prefix: ¥, Suffix: ""
        metadata.SymbolPrefix.CurrentValue.ShouldBe("¥");
        metadata.SymbolSuffix.CurrentValue.ShouldBe("");

        // Act & Assert: Update to JPY, ja-JP -> Prefix: "", Suffix: "円"
        config.System.CultureCode = "ja-JP";
        ConfigurationLoader.Save(config, tempFile);
        provider.Reload();

        metadata.SymbolPrefix.CurrentValue.ShouldBe("");
        metadata.SymbolSuffix.CurrentValue.ShouldBe("円");

        // Act & Assert: Update to USD, en-US -> Prefix: "$", Suffix: ""
        config.System.CurrencyCode = "USD";
        config.System.CultureCode = "en-US";
        ConfigurationLoader.Save(config, tempFile);
        provider.Reload();

        metadata.SymbolPrefix.CurrentValue.ShouldBe("$");
        metadata.SymbolSuffix.CurrentValue.ShouldBe("");

        if (File.Exists(tempFile)) File.Delete(tempFile);
    }
}
