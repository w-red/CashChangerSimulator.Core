using MicroResolver;
using R3;

namespace CashChangerSimulator.Core.Configuration;

/// <summary>アプリケーション全体で共有される設定を提供するプロバイダー。</summary>
public class ConfigurationProvider
{
    private readonly Subject<Unit> _reloaded = new();
    /// <summary>設定が再読み込みされたときに通知されるストリーム。</summary>
    public Observable<Unit> Reloaded => _reloaded;

    /// <summary>現在保持している設定インスタンス。</summary>
    public SimulatorConfiguration Config { get; private set; }

    private readonly string? _configPath;

    /// <summary>デフォルト設定ファイルを読み込む。</summary>
    [Inject]
    public ConfigurationProvider() : this(null) { }

    /// <summary>指定されたパスの設定ファイルを読み込む。</summary>
    public ConfigurationProvider(string? configPath)
    {
        _configPath = configPath;
        Config = _configPath != null ? ConfigurationLoader.Load(_configPath) : ConfigurationLoader.Load();
    }

    /// <summary>設定ファイルを再読み込みして保持するインスタンスを更新する。</summary>
    public void Reload()
    {
        Config = _configPath != null ? ConfigurationLoader.Load(_configPath) : ConfigurationLoader.Load();
        _reloaded.OnNext(Unit.Default);
    }
}
