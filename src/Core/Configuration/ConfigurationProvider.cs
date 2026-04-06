using R3;

namespace CashChangerSimulator.Core.Configuration;

/// <summary>アプリケーション全体で共有される設定を提供するプロバイダー。.</summary>
/// <remarks>
/// 設定ファイル（config.toml）から読み込まれた `SimulatorConfiguration` を保持し、提供します。
/// 実行時の設定再読み込み（ホットリロード等）を管理し、変更通知を `Reloaded` ストリームで行います。.
/// </remarks>
public class ConfigurationProvider : IDisposable
{
    private readonly Subject<Unit> reloaded = new();
    private FileSystemWatcher? watcher;
    private DateTime lastRead = DateTime.MinValue;
    private string? configPath;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="ConfigurationProvider"/> class.デフォルト設定ファイルを読み込むプロバイダーを初期化します。.</summary>
    public ConfigurationProvider()
    {
        configPath = null;
        Config = ConfigurationLoader.Load();
        SetupWatcher(ConfigurationLoader.DefaultConfigFilePath);
    }

    /// <summary>Gets 設定が再読み込みされたときに通知されるストリーム。.</summary>
    public Observable<Unit> Reloaded => reloaded;

    /// <summary>Gets or sets 現在保持している設定インスタンス。.</summary>
    public virtual SimulatorConfiguration Config { get; protected set; }

    /// <summary>指定されたパスの設定ファイルを読み込むプロバイダーを作成します。.</summary>
    /// <param name="configFilePath">読み込む設定ファイルのパス。.</param>
    /// <returns>初期化されたプロバイダー。.</returns>
    public static ConfigurationProvider CreateWithFilePath(string configFilePath)
    {
        var provider = new ConfigurationProvider { configPath = configFilePath, Config = ConfigurationLoader.Load(configFilePath) };
        provider.SetupWatcher(configFilePath);
        return provider;
    }

    /// <summary>設定ファイルを再読み込みして保持するインスタンスを更新します。.</summary>
    public virtual void Reload()
    {
        Config = configPath != null ? ConfigurationLoader.Load(configPath) : ConfigurationLoader.Load();
        if (!disposed)
        {
            reloaded.OnNext(Unit.Default);
        }
    }

    /// <summary>設定インスタンスを直接更新します（主にテスト用）。.</summary>
    /// <param name="config">新しい設定インスタンス。.</param>
    public void Update(SimulatorConfiguration config)
    {
        Config = config;
        if (!disposed)
        {
            reloaded.OnNext(Unit.Default);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>リソースを解放します。.</summary>
    /// <param name="disposing">明示的な破棄かどうか。.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        if (disposing)
        {
            watcher?.Dispose();
            reloaded.OnCompleted();
            reloaded.Dispose();
        }

        disposed = true;
    }

    private void SetupWatcher(string path)
    {
        watcher?.Dispose();

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        var fileName = Path.GetFileName(fullPath);

        if (directory == null || !Directory.Exists(directory))
        {
            return;
        }

        watcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
        };

        watcher.Changed += OnFileChanged;
        watcher.Renamed += OnFileChanged;
        watcher.EnableRaisingEvents = true;
    }

    private void OnFileChanged(object? sender, FileSystemEventArgs e)
    {
        // Simple debounce to avoid multiple reloads for a single save
        if (DateTime.Now - lastRead < TimeSpan.FromMilliseconds(500))
        {
            return;
        }

        lastRead = DateTime.Now;

        // Give the file a moment to be released by the writer
        Thread.Sleep(100);
        Reload();
    }
}
