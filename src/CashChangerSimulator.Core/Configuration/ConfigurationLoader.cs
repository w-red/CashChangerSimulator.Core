using System;
using System.IO;
using System.Threading.Tasks;
using CsToml;
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
}
