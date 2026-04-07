using Microsoft.Extensions.Logging;
using Tomlyn;
using ZLogger;

namespace CashChangerSimulator.Core.Configuration;

/// <summary>TOML 形式の設定ファイルを管理するクラス。</summary>
public static class ConfigurationLoader
{
    private static readonly ILogger Logger = LogProvider
        .CreateLogger<SimulatorConfiguration>(); // SimulatorConfiguration as category since it's the main config class

    /// <summary>デフォルトの設定ファイルパス。</summary>
    private static readonly string DefaultConfigPath =
        Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "config.toml");

    /// <summary>在庫状態の保存先ファイルパス。</summary>
    private static readonly string InventoryStatePath =
        Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "inventory.toml");

    /// <summary>PascalCase プロパティ名をそのまま TOML キーとして使用するオプション。</summary>
    private static readonly TomlSerializerOptions ModelOptions = new()
    {
        PropertyNamingPolicy = null,
    };

    /// <summary>取引履歴の保存先ファイルパス（バイナリ形式）。</summary>
    private static readonly string HistoryStatePath =
        Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "history.bin");

    /// <summary>デフォルトの設定ファイルパス。</summary>
    public static string DefaultConfigFilePath =>
        DefaultConfigPath;

    /// <summary>デフォルトの在庫状態保存先ファイルパス。</summary>
    public static string DefaultInventoryStateFilePath =>
        InventoryStatePath;

    /// <summary>デフォルトの取引履歴保存先ファイルパス。</summary>
    public static string DefaultHistoryStateFilePath =>
        HistoryStatePath;

    /// <summary>設定ファイルを読み込む（存在しない場合はデフォルトを作成して返す）。</summary>
    /// <param name="path">読み込み元のファイルパス。</param>
    /// <returns>読み込まれた設定。</returns>
    public static SimulatorConfiguration Load(
        string? path = null)
    {
        var filePath = path ?? DefaultConfigPath;
        try
        {
            if (!File.Exists(filePath))
            {
                var defaultConfig = new SimulatorConfiguration();
                try
                {
                    Save(defaultConfig, filePath);
                }
                catch
                {
                    // Ignore save error when just loading defaults
                }

                return defaultConfig;
            }

            var tomlText = File.ReadAllText(filePath);
            var config = TomlSerializer
                .Deserialize<SimulatorConfiguration>(
                    tomlText,
                    options: ModelOptions)
                ?? new SimulatorConfiguration();

            config.System ??= new SystemSettings();
            config.Logging ??= new LoggingSettings();
            config.Simulation ??= new SimulationSettings();
            config.Thresholds ??= new ThresholdSettings();

            return config;
        }
        catch (IOException ex)
        {
            Logger.ZLogError(
                ex,
                $"IO Error loading configuration from {filePath}. Returning default.");
            return new SimulatorConfiguration();
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.ZLogError(
                ex,
                $"Access denied loading configuration from {filePath}. Returning default.");
            return new SimulatorConfiguration();
        }
        catch (TomlException ex)
        {
            Logger.ZLogError(
                ex,
                $"Invalid TOML format in {filePath}. Returning default.");
            return new SimulatorConfiguration();
        }
    }

    /// <summary>設定をファイルへシリアライズして保存する。</summary>
    /// <param name="config">保存する設定オブジェクト。</param>
    /// <param name="path">保存先のファイルパス。</param>
    public static void Save(
        SimulatorConfiguration config,
        string? path = null)
    {
        var filePath = path ?? DefaultConfigPath;
        var tomlText = TomlSerializer
            .Serialize(config, ModelOptions);
        File.WriteAllText(filePath, tomlText);
    }

    /// <summary>在庫状態を読み込む。</summary>
    /// <param name="path">読み込み元のファイルパス。</param>
    /// <returns>読み込まれた在庫状態。</returns>
    public static InventoryState LoadInventoryState(
        string? path = null)
    {
        var filePath = path ?? InventoryStatePath;
        try
        {
            if (!File.Exists(filePath))
            {
                return new InventoryState();
            }

            var tomlText = File.ReadAllText(filePath);
            var state = TomlSerializer
                .Deserialize<InventoryState>(
                    tomlText,
                    options: ModelOptions)
                ?? new InventoryState();
            return state;
        }
        catch (IOException ex)
        {
            Logger.ZLogError(
                ex,
                $"IO Error loading inventory state from {filePath}. Returning empty state.");
            return new InventoryState();
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.ZLogError(
                ex,
                $"Access denied loading inventory state from {filePath}. Returning empty state.");
            return new InventoryState();
        }
        catch (TomlException ex)
        {
            Logger.ZLogError(
                ex,
                $"Invalid TOML format in inventory state {filePath}. Returning empty state.");
            return new InventoryState();
        }
    }

    /// <summary>在庫状態を保存する。</summary>
    /// <param name="state">保存する在庫状態。</param>
    /// <param name="path">保存先のファイルパス。</param>
    public static void SaveInventoryState(InventoryState state, string? path = null)
    {
        var filePath = path ?? InventoryStatePath;
        var tomlText = TomlSerializer
            .Serialize(state, ModelOptions);
        File.WriteAllText(filePath, tomlText);
    }
}
