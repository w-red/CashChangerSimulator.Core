using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using Shouldly;
using Tomlyn;

namespace CashChangerSimulator.Tests.Core.Configuration;

/// <summary>ConfigurationLoader の永続化とエラーハンドリングを検証するテスト。</summary>
public class ConfigurationLoaderTests : IDisposable
{
    private readonly string testConfigPath = "test_config.toml";
    private readonly string testInventoryPath = "test_inventory.toml";

    /// <summary>Initializes a new instance of the <see cref="ConfigurationLoaderTests"/> class.ConfigurationLoaderTests の新しいインスタンスを初期化します。</summary>
    public ConfigurationLoaderTests()
    {
        CleanupFiles();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        CleanupFiles();
        GC.SuppressFinalize(this);
    }

    private void CleanupFiles()
    {
        if (File.Exists(testConfigPath))
        {
            File.Delete(testConfigPath);
        }

        if (File.Exists(testInventoryPath))
        {
            File.Delete(testInventoryPath);
        }
        
        var defaultInv = ConfigurationLoader.DefaultInventoryStateFilePath;
        if (File.Exists(defaultInv))
        {
            // We don't want to delete the real default if it exists, but for testing we might need to.
            // In a CI environment this is usually fine.
        }
    }

    /// <summary>設定ファイルが存在しない場合にデフォルト値が返されることを検証する。</summary>
    [Fact]
    public void LoadConfigShouldReturnDefaultsWhenFileNotFound()
    {
        // Act
        var config = ConfigurationLoader.Load(testConfigPath);

        // Assert
        config.ShouldNotBeNull();
        config.System.CurrencyCode.ShouldBe("JPY");
        File.Exists(testConfigPath).ShouldBeTrue(); // Created default
    }

    /// <summary>設定ファイルが破損している場合にデフォルト値が返されることを検証する。</summary>
    [Fact]
    public void LoadConfigShouldReturnDefaultsWhenCorrupted()
    {
        // Arrange
        File.WriteAllText(testConfigPath, "THIS IS NOT TOML !!!");

        // Act
        var config = ConfigurationLoader.Load(testConfigPath);

        // Assert
        config.ShouldNotBeNull();
        config.System.CurrencyCode.ShouldBe("JPY"); // Fallback to default
    }

    /// <summary>設定の保存と読み込みで値が維持されることを検証する。</summary>
    [Fact]
    public void SaveAndLoadConfigShouldMaintainValues()
    {
        // Arrange
        var config = new SimulatorConfiguration();
        config.System.CurrencyCode = "USD";
        config.Inventory["USD"] = new InventorySettings();

        // Act
        ConfigurationLoader.Save(config, testConfigPath);
        var loaded = ConfigurationLoader.Load(testConfigPath);

        // Assert
        loaded.System.CurrencyCode.ShouldBe("USD");
        loaded.Inventory.ContainsKey("USD").ShouldBeTrue();
    }

    /// <summary>在庫状態ファイルが存在しない場合に空の状態が返されることを検証する。</summary>
    [Fact]
    public void LoadInventoryStateShouldReturnEmptyWhenFileNotFound()
    {
        // Act
        var state = ConfigurationLoader.LoadInventoryState("non_existent_inventory.toml");

        // Assert
        state.ShouldNotBeNull();
        state.Counts.ShouldBeEmpty();
    }

    /// <summary>在庫状態の保存と読み込みで値が維持されることを検証する。</summary>
    [Fact]
    public void SaveAndLoadInventoryStateShouldMaintainValues()
    {
        // Arrange
        var state = new InventoryState();
        state.Counts["JPY:B1000"] = 10;

        // Act
        ConfigurationLoader.SaveInventoryState(state, testInventoryPath);
        var loaded = ConfigurationLoader.LoadInventoryState(testInventoryPath);

        // Assert
        loaded.Counts["JPY:B1000"].ShouldBe(10);
    }

    /// <summary>デフォルトのパスが正しく取得できることを検証します。</summary>
    [Fact]
    public void DefaultPathsShouldBeCorrect()
    {
        ConfigurationLoader.DefaultConfigFilePath.ShouldNotBeNullOrEmpty();
        ConfigurationLoader.DefaultInventoryStateFilePath.ShouldNotBeNullOrEmpty();
        ConfigurationLoader.DefaultHistoryStateFilePath.ShouldNotBeNullOrEmpty();
    }

    /// <summary>空の TOML ファイルから設定を読み込んだ際にデフォルトが返されることを検証します。</summary>
    [Fact]
    public void LoadConfigWithEmptyTomlShouldReturnDefault()
    {
        File.WriteAllText(testConfigPath, "");
        var config = ConfigurationLoader.Load(testConfigPath);
        config.ShouldNotBeNull();
        config.System.ShouldNotBeNull();
    }

    /// <summary>空の TOML ファイルから在庫状態を読み込んだ際に空の状態が返されることを検証します。</summary>
    [Fact]
    public void LoadInventoryStateWithEmptyTomlShouldReturnEmpty()
    {
        File.WriteAllText(testInventoryPath, "");
        var state = ConfigurationLoader.LoadInventoryState(testInventoryPath);
        state.ShouldNotBeNull();
        state.Counts.ShouldBeEmpty();
    }

    /// <summary>在庫状態の読み込み時に例外が発生した場合に空の状態が返されることを検証する。</summary>
    [Fact]
    public void LoadInventoryStateShouldHandleExceptions()
    {
        File.WriteAllText(testInventoryPath, "INVALID = [[");

        // This causes TomlException which is caught.
        var state = ConfigurationLoader.LoadInventoryState(testInventoryPath);
        state.ShouldNotBeNull();
        state.Counts.ShouldBeEmpty();
    }

    /// <summary>ファイルアクセス拒否（UnauthorizedAccessException）発生時にデフォルト値が返されることを検証する。</summary>
    [Fact]
    public void LoadConfigShouldReturnDefaultsWhenAccessDenied()
    {
        // ディレクトリパスを渡すと ReadAllText で UnauthorizedAccessException が発生する (Windows)
        var tempDir = Path.Combine(Path.GetTempPath(), $"dir_config_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var config = ConfigurationLoader.Load(tempDir);
            config.ShouldNotBeNull();
        }
        finally
        {
            Directory.Delete(tempDir);
        }
    }

    /// <summary>一部のセクションが欠落している TOML から設定を読み込んだ際に、欠落セクションが初期化されることを検証します。</summary>
    [Fact]
    public void LoadConfigWithPartialTomlShouldInitializeSections()
    {
        // [System] だけがあり、他がない状態
        File.WriteAllText(testConfigPath, "[System]\nCurrencyCode = \"USD\"");
        
        var config = ConfigurationLoader.Load(testConfigPath);
        
        config.ShouldNotBeNull();
        config.System.CurrencyCode.ShouldBe("USD");
        config.Logging.ShouldNotBeNull();
        config.Simulation.ShouldNotBeNull();
        config.Thresholds.ShouldNotBeNull();
    }

    /// <summary>存在しない金種の設定を取得しようとした際に、デフォルト値が返されることを検証します。</summary>
    [Fact]
    public void GetDenominationSettingShouldReturnFallbackWhenMissing()
    {
        var config = new SimulatorConfiguration();
        var key = new DenominationKey(123m, CurrencyCashType.Bill, "ABC");
        
        var setting = config.GetDenominationSetting(key);
        
        setting.ShouldNotBeNull();
        setting.NearEmpty.ShouldBe(config.Thresholds.NearEmpty);
        setting.IsRecyclable.ShouldBeTrue(); // Default
    }

    /// <summary>Save メソッドが設定をファイルに正しく書き込めることを検証します。</summary>
    [Fact]
    public void SaveShouldWriteToFile()
    {
        var config = new SimulatorConfiguration();
        config.System.CurrencyCode = "EUR";
        
        ConfigurationLoader.Save(config, testConfigPath);
        
        File.Exists(testConfigPath).ShouldBeTrue();
        var content = File.ReadAllText(testConfigPath);
        content.ShouldContain("CurrencyCode = \"EUR\"");
    }

    /// <summary>在庫状態の保存と読み込みが正しく動作することを検証します。</summary>
    [Fact]
    public void LoadInventoryStateShouldWork()
    {
        var state = new InventoryState();
        state.Counts["JPY:B1000"] = 55;
        
        ConfigurationLoader.SaveInventoryState(state, testConfigPath);
        var loaded = ConfigurationLoader.LoadInventoryState(testConfigPath);
        
        loaded.Counts["JPY:B1000"].ShouldBe(55);
    }

    /// <summary>存在しない在庫状態ファイルを読み込んだ際に、空の状態が返されることを検証します。</summary>
    [Fact]
    public void LoadInventoryStateShouldReturnEmptyOnFileNotFound()
    {
        var loaded = ConfigurationLoader.LoadInventoryState("non_existent_inventory.toml");
        loaded.ShouldNotBeNull();
        loaded.Counts.ShouldBeEmpty();
    }

    /// <summary>不正な TOML 形式の在庫状態ファイルを読み込んだ際に、空の状態が返されることを検証します。</summary>
    [Fact]
    public void LoadInventoryStateShouldReturnEmptyOnInvalidToml()
    {
        File.WriteAllText(testConfigPath, "!!! INVALID TOML !!!");
        var loaded = ConfigurationLoader.LoadInventoryState(testConfigPath);
        loaded.ShouldNotBeNull();
        loaded.Counts.ShouldBeEmpty();
    }
}
