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
        config.CurrencyCode.ShouldBe("JPY");
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
        config.CurrencyCode.ShouldBe("JPY"); // Fallback to default
    }

    /// <summary>設定の保存と読み込みで値が維持されることを検証する。</summary>
    [Fact]
    public void SaveAndLoadConfigShouldMaintainValues()
    {
        // Arrange
        var config = new SimulatorConfiguration { CurrencyCode = "USD" };
        config.Inventory["USD"] = new InventorySettings();
        
        // Act
        ConfigurationLoader.Save(config, _testConfigPath);
        var loaded = ConfigurationLoader.Load(_testConfigPath);

        // Assert
        loaded.CurrencyCode.ShouldBe("USD");
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

    /// <summary>履歴状態ファイルが存在しない場合に初期状態が返されることを検証する。</summary>
    [Fact]
    public void LoadHistoryStateShouldReturnInitialWhenFileNotFound()
    {
        // Act
        var state = ConfigurationLoader.LoadHistoryState();

        // Assert
        state.ShouldNotBeNull();
        state.Entries.Count.ShouldBe(1);
        state.Entries[0].Type.ShouldBe(TransactionType.Unknown);
    }

    /// <summary>履歴状態の保存と読み込みで値が維持されることを検証する。</summary>
    [Fact]
    public void SaveAndLoadHistoryStateShouldMaintainValues()
    {
        // Arrange
        var state = new HistoryState
        {
            Entries =
            [
                new HistoryEntryState { Amount = 1000, Type = TransactionType.Deposit, Timestamp = DateTimeOffset.Now, Counts = new Dictionary<string, int> { { "JPY:B1000", 1 } } }
            ]
        };

        // Act
        ConfigurationLoader.SaveHistoryState(state);
        var loaded = ConfigurationLoader.LoadHistoryState();

        // Assert
        loaded.Entries.Count.ShouldBe(1);
        loaded.Entries[0].Amount.ShouldBe(1000);
        loaded.Entries[0].Counts["JPY:B1000"].ShouldBe(1);
    }
}
