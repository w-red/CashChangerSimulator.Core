using Microsoft.Extensions.Logging;
using Tomlyn;
using ZLogger;

namespace CashChangerSimulator.Core.Configuration;

/// <summary>TOML 形式の設定ファイルを管理するクラス。</summary>
public static class ConfigurationLoader
{
    private static readonly ILogger Logger = LogProvider.CreateLogger<SimulatorConfiguration>(); // SimulatorConfiguration as category since it's the main config class
    /// <summary>デフォルトの設定ファイルパス。</summary>
    private static readonly string DefaultConfigPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "config.toml");

    public static string GetDefaultConfigPath() => DefaultConfigPath;

    /// <summary>在庫状態の保存先ファイルパス。</summary>
    private static readonly string InventoryStatePath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "inventory.toml");

    public static string GetDefaultInventoryStatePath() => InventoryStatePath;

    /// <summary>PascalCase プロパティ名をそのまま TOML キーとして使用するオプション。</summary>
    private static readonly TomlSerializerOptions ModelOptions = new()
    {
        PropertyNamingPolicy = null
    };

    /// <summary>設定ファイルを読み込む（存在しない場合はデフォルトを作成して返す）。</summary>
    public static SimulatorConfiguration Load(string? path = null)
    {
        var filePath = path ?? DefaultConfigPath;
        if (!File.Exists(filePath))
        {
            var defaultConfig = new SimulatorConfiguration();
            Save(defaultConfig, filePath);
            return defaultConfig;
        }

        try
        {
            var tomlText = File.ReadAllText(filePath);
            var config = TomlSerializer.Deserialize<SimulatorConfiguration>(tomlText, options: ModelOptions) ?? new SimulatorConfiguration();

            config.System ??= new SystemSettings();
            config.Logging ??= new LoggingSettings();
            config.Simulation ??= new SimulationSettings();
            config.Thresholds ??= new ThresholdSettings();
            config.Inventory ??= [];

            return config;
        }
        catch (Exception ex)
        {
            Logger.ZLogError(
                ex,
                $"Failed to load configuration from {filePath}. Returning default configuration.");
            // Return default configuration instead of crashing the app
            return new SimulatorConfiguration();
        }
    }

    /// <summary>設定をファイルへシリアライズして保存する。</summary>
    public static void Save(SimulatorConfiguration config, string? path = null)
    {
        var filePath = path ?? DefaultConfigPath;
        var tomlText = TomlSerializer.Serialize(config, ModelOptions);
        File.WriteAllText(filePath, tomlText);
    }

    /// <summary>在庫状態を読み込む。</summary>
    public static InventoryState LoadInventoryState(string? path = null)
    {
        var filePath = path ?? InventoryStatePath;
        if (!File.Exists(filePath))
        {
            return new InventoryState();
        }

        try
        {
            var tomlText = File.ReadAllText(filePath);
            var state = TomlSerializer.Deserialize<InventoryState>(tomlText, options: ModelOptions) ?? new InventoryState();
            state.EnsureInitialized();
            return state;
        }
        catch (Exception ex)
        {
            Logger.ZLogError(ex, $"Failed to load inventory state from {filePath}. Returning empty state.");
            // Return empty instead of crashing; allows starting with zero inventory
            return new InventoryState();
        }
    }

    /// <summary>在庫状態を保存する。</summary>
    public static void SaveInventoryState(InventoryState state, string? path = null)
    {
        var filePath = path ?? InventoryStatePath;
        var tomlText = TomlSerializer.Serialize(state, ModelOptions);
        File.WriteAllText(filePath, tomlText);
    }

    /// <summary>取引履歴の保存先ファイルパス（バイナリ形式）。</summary>
    private static readonly string HistoryStatePath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "history.bin");

    public static string GetDefaultHistoryStatePath() => HistoryStatePath;

    private static HistoryState CreateEmptyHistoryState()
    {
        return new HistoryState { Entries = [] };
    }
}
