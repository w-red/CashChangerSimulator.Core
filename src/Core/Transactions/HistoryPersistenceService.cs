using CashChangerSimulator.Core.Configuration;
using MemoryPack;
using Microsoft.Extensions.Logging;
using R3;

namespace CashChangerSimulator.Core.Transactions;

/// <summary>取引履歴の永続化を担当するサービス。</summary>
public class HistoryPersistenceService : IDisposable
{
    private readonly TransactionHistory _history;
    private readonly string _filePath;
    private readonly ILogger<HistoryPersistenceService> _logger;
    private readonly CompositeDisposable _disposables = new();

    public HistoryPersistenceService(TransactionHistory history, string? filePath = null)
    {
        _history = history;
        _filePath = filePath ?? ConfigurationLoader.GetDefaultHistoryStatePath();
        _logger = LogProvider.CreateLogger<HistoryPersistenceService>();
    }

    /// <summary>履歴ファイルを読み込みます。</summary>
    public HistoryState Load()
    {
        if (!File.Exists(_filePath))
        {
            return new HistoryState { Entries = [] };
        }

        try
        {
            var bin = File.ReadAllBytes(_filePath);
            var state = MemoryPackSerializer.Deserialize<HistoryState>(bin);
            return state ?? new HistoryState { Entries = [] };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load history from {Path}. The file might be corrupted or inaccessible.", _filePath);
            return new HistoryState { Entries = [] };
        }
    }

    /// <summary>履歴ファイルを保存します。</summary>
    public void Save(HistoryState state)
    {
        try
        {
            var bin = MemoryPackSerializer.Serialize(state);
            File.WriteAllBytes(_filePath, bin);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save history to {Path}. Possible reasons: disk full, permission denied, or file in use.", _filePath);
            // NOTE: In a production environment, we might want to implement a retry policy or save to a backup location.
        }
    }

    /// <summary>オートセーブを開始します。取引が追加されるたびに保存されます。</summary>
    public void StartAutoSave()
    {
        _history.Added
            .Subscribe(_ =>
            {
                Save(_history.ToState());
            })
            .AddTo(_disposables);
    }

    public void Dispose()
    {
        _disposables.Dispose();
        GC.SuppressFinalize(this);
    }
}
