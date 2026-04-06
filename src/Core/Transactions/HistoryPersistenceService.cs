using CashChangerSimulator.Core.Configuration;
using MemoryPack;
using Microsoft.Extensions.Logging;
using R3;
using ZLogger;

namespace CashChangerSimulator.Core.Transactions;

/// <summary>取引履歴の永続化を担当するサービス。.</summary>
public class HistoryPersistenceService : IDisposable
{
    private readonly TransactionHistory history;
    private readonly string filePath;
    private readonly ILogger<HistoryPersistenceService> logger;
    private readonly CompositeDisposable disposables = new();
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="HistoryPersistenceService"/> class.
    /// </summary>
    /// <param name="history">取引履歴オブジェクト。.</param>
    /// <param name="filePath">保存先のファイルパス。null の場合はデフォルトパスが使用される。.</param>
    public HistoryPersistenceService(TransactionHistory history, string? filePath = null)
    {
        this.history = history;
        this.filePath = filePath ?? ConfigurationLoader.DefaultHistoryStateFilePath;
        logger = LogProvider.CreateLogger<HistoryPersistenceService>();
    }

    /// <summary>履歴ファイルを読み込みます。.</summary>
    /// <returns>読み込まれた履歴の状態。ファイルがない場合は空の状態を返す。.</returns>
    public HistoryState Load()
    {
        if (!File.Exists(filePath))
        {
            return new HistoryState { Entries = [] };
        }

        try
        {
            var bin = File.ReadAllBytes(filePath);
            var state = MemoryPackSerializer.Deserialize<HistoryState>(bin);
            return state ?? new HistoryState { Entries = [] };
        }
        catch (IOException ex)
        {
            logger.ZLogError(ex, $"IO Error loading history from {filePath}. Returning empty.");
            return new HistoryState { Entries = [] };
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.ZLogError(ex, $"Access denied loading history from {filePath}. Returning empty.");
            return new HistoryState { Entries = [] };
        }
        catch (MemoryPackSerializationException ex)
        {
            logger.ZLogError(ex, $"Invalid binary format in history {filePath}. Returning empty.");
            return new HistoryState { Entries = [] };
        }
    }

    /// <summary>履歴ファイルを保存します。.</summary>
    /// <param name="state">保存する履歴の状態。.</param>
    public void Save(HistoryState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        try
        {
            var bin = MemoryPackSerializer.Serialize(state);
            File.WriteAllBytes(filePath, bin);
        }
        catch (IOException ex)
        {
            logger.ZLogError(ex, $"IO Error saving history to {filePath}.");
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.ZLogError(ex, $"Access denied saving history to {filePath}.");
        }
    }

    /// <summary>オートセーブを開始します。取引が追加されるたびに保存されます。.</summary>
    public void StartAutoSave()
    {
        history.Added
            .Subscribe(this, (addedEntry, state) =>
            {
                state.Save(state.history.ToState());
            })
            .AddTo(disposables);
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
            disposables.Dispose();
        }

        disposed = true;
    }
}
