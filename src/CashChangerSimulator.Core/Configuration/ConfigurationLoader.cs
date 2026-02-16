using CsToml.Extensions;

namespace CashChangerSimulator.Core.Configuration;

/// <summary>
/// TOML 形式の設定ファイルを管理するクラス。
/// </summary>
public static class ConfigurationLoader
{
    /// <summary>デフォルトの設定ファイルパス。</summary>
    private static readonly string DefaultConfigPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "config.toml");

    /// <summary>在庫状態の保存先ファイルパス。</summary>
    private static readonly string InventoryStatePath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "inventory.toml");

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
            return CsTomlFileSerializer.Deserialize<SimulatorConfiguration>(filePath);
        }
        catch (Exception)
        {
            return new SimulatorConfiguration();
        }
    }

    /// <summary>設定をファイルへシリアライズして保存する。</summary>
    public static void Save(SimulatorConfiguration config, string? path = null)
    {
        var filePath = path ?? DefaultConfigPath;
        CsTomlFileSerializer.Serialize(filePath, config);
    }

    /// <summary>在庫状態を読み込む。</summary>
    public static InventoryState LoadInventoryState()
    {
        if (!File.Exists(InventoryStatePath))
        {
            return new InventoryState();
        }

        try
        {
            return CsTomlFileSerializer.Deserialize<InventoryState>(InventoryStatePath);
        }
        catch (Exception)
        {
            return new InventoryState();
        }
    }

    /// <summary>在庫状態を保存する。</summary>
    public static void SaveInventoryState(InventoryState state)
    {
        CsTomlFileSerializer.Serialize(InventoryStatePath, state);
    }
}
