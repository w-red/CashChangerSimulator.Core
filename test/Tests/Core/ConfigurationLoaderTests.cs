using CashChangerSimulator.Core.Configuration;
using Shouldly;
using Tomlyn;

namespace CashChangerSimulator.Tests.Core;

/// <summary>ConfigurationLoader の永続化とエラーハンドリングを検証するテスト。</summary>
public class ConfigurationLoaderTests : IDisposable
{
    private readonly string testConfigPath = "test_config.toml";
    private readonly string testInventoryPath = "inventory.toml"; // Fixed path in loader

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
        var state = ConfigurationLoader.LoadInventoryState();

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
        ConfigurationLoader.SaveInventoryState(state);
        var loaded = ConfigurationLoader.LoadInventoryState();

        // Assert
        loaded.Counts["JPY:B1000"].ShouldBe(10);
    }

    /// <summary>設定ファイルが構文エラーなどで破損している場合に、デフォルトの設定が返されることを検証します。</summary>
    [Fact]
    public void LoadCorruptedFileShouldReturnDefault()
    {
        File.WriteAllText(testConfigPath, "INVALID TOML [[");
        var config = ConfigurationLoader.Load(testConfigPath);
        config.ShouldNotBeNull();
        config.Inventory.ShouldNotBeEmpty();
    }

    /// <summary>カスタムパスを指定して設定を保存および読み込んだ際に、データが正しく保持されることを検証します。</summary>
    [Fact]
    public void SaveAndLoadShouldPreserveData()
    {
        var path = "custom_config.toml";
        var config = new SimulatorConfiguration();
        config.System.CurrencyCode = "USD";
        ConfigurationLoader.Save(config, path);

        var loaded = ConfigurationLoader.Load(path);
        loaded.System.CurrencyCode.ShouldBe("USD");
        File.Delete(path);
    }

    /// <summary>存在しないパスから在庫状態を読み込もうとした際に、空の状態が返されることを検証します。</summary>
    [Fact]
    public void LoadInventoryStateNonExistentShouldReturnEmpty()
    {
        var path = "non_existent.inv";
        var state = ConfigurationLoader.LoadInventoryState(path);
        state.ShouldNotBeNull();
        state.Counts.ShouldBeEmpty();
    }

    /// <summary>Tomlyn のシリアライズとデシリアライズの挙動を詳細に検証します。</summary>
    [Fact]
    public void DiagnosticTomlynTest()
    {
        var state = new InventoryState();
        state.Counts["JPY:B1000"] = 5;

        var options = new TomlSerializerOptions
        {
            PropertyNamingPolicy = null,
        };

        var toml = TomlSerializer.Serialize(state, options);
        Console.WriteLine($"--- Generated TOML ---\n{toml}\n----------------------");

        var loaded = TomlSerializer.Deserialize<InventoryState>(toml, options);
        loaded.ShouldNotBeNull();
        loaded.Counts.ShouldNotBeNull();
        if (!loaded.Counts.ContainsKey("JPY:B1000"))
        {
            Console.WriteLine("Key JPY:B1000 MISSING in loaded Counts!");
            foreach (var key in loaded.Counts.Keys)
            {
                Console.WriteLine($"Found key: '{key}'");
            }
        }

        loaded.Counts["JPY:B1000"].ShouldBe(5);
    }
}
