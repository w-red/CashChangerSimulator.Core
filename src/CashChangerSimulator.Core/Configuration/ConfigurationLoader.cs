using CsToml.Extensions;
using CashChangerSimulator.Core.Models;

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
            var config = CsTomlFileSerializer.Deserialize<SimulatorConfiguration>(filePath);
            
            // 後方互換性：[Inventory.Denominations] という旧形式を [Inventory.JPY.Denominations] などへ移行
            if (config.Inventory.TryGetValue("Denominations", out var legacySettings))
            {
                var targetCurrency = string.IsNullOrEmpty(config.CurrencyCode) ? "JPY" : config.CurrencyCode;
                
                // 現在の通貨コードの在庫設定が（デフォルトなどで）存在しないか、空の場合のみ上書き移行
                if (!config.Inventory.ContainsKey(targetCurrency) || config.Inventory[targetCurrency].Denominations.Count == 0)
                {
                    config.Inventory[targetCurrency] = legacySettings;
                }
                
                config.Inventory.Remove("Denominations");
                
                // 移行が発生したため、新形式で上書き保存
                Save(config, filePath);
            }

            config.Simulation ??= new SimulationSettings();
            config.Logging ??= new LoggingSettings();
            return config;
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
            var state = CsTomlFileSerializer.Deserialize<InventoryState>(InventoryStatePath);
            state.EnsureInitialized();
            return state;
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

    /// <summary>取引履歴の保存先ファイルパス（バイナリ形式）。</summary>
    private static readonly string HistoryStatePath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "history.bin");

    /// <summary>取引履歴を読み込む。</summary>
    public static HistoryState LoadHistoryState()
    {
        if (!File.Exists(HistoryStatePath))
        {
            return CreateInitialHistoryState();
        }

        try
        {
            var bin = File.ReadAllBytes(HistoryStatePath);
            var state = MemoryPack.MemoryPackSerializer.Deserialize<HistoryState>(bin) ?? CreateInitialHistoryState();
            state.Entries ??= [];
            return state;
        }
        catch (Exception)
        {
            return CreateInitialHistoryState();
        }
    }

    /// <summary>取引履歴を保存する。</summary>
    public static void SaveHistoryState(HistoryState state)
    {
        try
        {
            var bin = MemoryPack.MemoryPackSerializer.Serialize(state);
            File.WriteAllBytes(HistoryStatePath, bin);
        }
        catch (Exception)
        {
            // Logging can be added here
        }
    }

    private static HistoryState CreateInitialHistoryState()
    {
        return new HistoryState
        {
            Entries = [
                new HistoryEntryState
                {
                    Timestamp = DateTimeOffset.Now,
                    Type = TransactionType.Unknown,
                    Amount = 0,
                    Counts = new Dictionary<string, int>()
                }
            ]
        };
    }
}
