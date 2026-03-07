using R3;

namespace CashChangerSimulator.Core.Configuration;

/// <summary>アプリケーション全体で共有される設定を提供するプロバイダー。</summary>
/// <remarks>
/// 設定ファイル（config.toml）から読み込まれた `SimulatorConfiguration` を保持し、提供します。
/// 実行時の設定再読み込み（ホットリロード等）を管理し、変更通知を `Reloaded` ストリームで行います。
/// </remarks>
public class ConfigurationProvider
{
    private readonly Subject<Unit> _reloaded = new();
    
    /// <summary>設定が再読み込みされたときに通知されるストリーム。</summary>
    public Observable<Unit> Reloaded => _reloaded;

    /// <summary>現在保持している設定インスタンス。</summary>
    public SimulatorConfiguration Config { get; private set; }

    private string? _configPath;

    /// <summary>デフォルト設定ファイルを読み込むプロバイダーを初期化します。</summary>
    public ConfigurationProvider()
    {
        _configPath = null;
        Config = ConfigurationLoader.Load();
    }

    /// <summary>指定されたパスの設定ファイルを読み込むプロバイダーを作成します。</summary>
    public static ConfigurationProvider CreateWithFilePath(string configPath)
    {
        return new ConfigurationProvider { _configPath = configPath, Config = ConfigurationLoader.Load(configPath) };
    }

    /// <summary>設定ファイルを再読み込みして保持するインスタンスを更新します。</summary>
    public void Reload()
    {
        Config = _configPath != null ? ConfigurationLoader.Load(_configPath) : ConfigurationLoader.Load();
        _reloaded.OnNext(Unit.Default);
    }

    /// <summary>設定インスタンスを直接更新します（主にテスト用）。</summary>
    public void Update(SimulatorConfiguration config)
    {
        Config = config;
        _reloaded.OnNext(Unit.Default);
    }
}
