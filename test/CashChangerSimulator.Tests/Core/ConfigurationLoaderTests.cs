using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Transactions;
using Shouldly;

namespace CashChangerSimulator.Tests.Core;

/// <summary>ConfigurationLoader の永続化とエラーハンドリングを検証するテスト。</summary>
public class ConfigurationLoaderTests : IDisposable
{
    private readonly string _testConfigPath = "test_config.toml";
    private readonly string _testInventoryPath = "inventory.toml"; // Fixed path in loader
    private readonly string _testHistoryPath = "history.bin";     // Fixed path in loader

    /// <summary>ConfigurationLoaderTests の新しいインスタンスを初期化します。</summary>
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
        if (File.Exists(_testConfigPath)) File.Delete(_testConfigPath);
        if (File.Exists(_testInventoryPath)) File.Delete(_testInventoryPath);
        if (File.Exists(_testHistoryPath)) File.Delete(_testHistoryPath);
    }

    /// <summary>設定ファイルが存在しない場合にデフォルト値が返されることを検証する。</summary>
    [Fact]
    public void LoadConfigShouldReturnDefaultsWhenFileNotFound()
    {
        // Act
        var config = ConfigurationLoader.Load(_testConfigPath);

        // Assert
        config.ShouldNotBeNull();
        config.System.CurrencyCode.ShouldBe("JPY");
        File.Exists(_testConfigPath).ShouldBeTrue(); // Created default
    }

    /// <summary>設定ファイルが破損している場合にデフォルト値が返されることを検証する。</summary>
    [Fact]
    public void LoadConfigShouldReturnDefaultsWhenCorrupted()
    {
        // Arrange
        File.WriteAllText(_testConfigPath, "THIS IS NOT TOML !!!");

        // Act
        var config = ConfigurationLoader.Load(_testConfigPath);

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
        ConfigurationLoader.Save(config, _testConfigPath);
        var loaded = ConfigurationLoader.Load(_testConfigPath);

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

    [Fact]
    public void Load_CorruptedFile_ShouldReturnDefault()
    {
        File.WriteAllText(_testConfigPath, "INVALID TOML [[");
        var config = ConfigurationLoader.Load(_testConfigPath);
        config.ShouldNotBeNull();
        config.Inventory.ShouldNotBeEmpty();
    }

    [Fact]
    public void SaveAndLoad_ShouldPreserveData()
    {
        var path = "custom_config.toml";
        var config = new SimulatorConfiguration();
        config.System.CurrencyCode = "USD";
        ConfigurationLoader.Save(config, path);

        var loaded = ConfigurationLoader.Load(path);
        loaded.System.CurrencyCode.ShouldBe("USD");
        File.Delete(path);
    }

    [Fact]
    public void LoadInventoryState_NonExistent_ShouldReturnEmpty()
    {
        var path = "non_existent.inv";
        var state = ConfigurationLoader.LoadInventoryState(path);
        state.ShouldNotBeNull();
        state.Counts.ShouldBeEmpty();
    }

    [Fact]
    public void SaveAndLoadInventory_ShouldPreserveData()
    {
        var path = "custom_inventory.toml";
        var state = new InventoryState();
        state.Counts["B1000"] = 5;
        ConfigurationLoader.SaveInventoryState(state, path);

        var loaded = ConfigurationLoader.LoadInventoryState(path);
        loaded.Counts.ShouldContainKey("B1000");
        loaded.Counts["B1000"].ShouldBe(5);
        File.Delete(path);
    }

    [Fact]
    public void LoadHistoryState_Corrupted_ShouldReturnInitial()
    {
        var path = "corrupted_history.bin";
        File.WriteAllBytes(path, [0, 1, 2, 3, 4, 5]);
        var state = ConfigurationLoader.LoadHistoryState(path);
        state.ShouldNotBeNull();
        state.Entries.Count.ShouldBe(1);
        File.Delete(path);
    }
}
