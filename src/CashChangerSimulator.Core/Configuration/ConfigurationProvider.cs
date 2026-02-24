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

    /// <summary>設定を初期読み込みしてインスタンスを生成する。</summary>
    public ConfigurationProvider()
    {
        Config = ConfigurationLoader.Load();
    }

    /// <summary>設定ファイルを再読み込みして保持するインスタンスを更新する。</summary>
    public void Reload()
    {
        Config = ConfigurationLoader.Load();
        _reloaded.OnNext(Unit.Default);
    }
}
