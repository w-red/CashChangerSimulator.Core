using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Transactions;
using Shouldly;

namespace CashChangerSimulator.Tests.Transactions;

/// <summary>取引履歴の永続化サービスを検証するテストクラス。</summary>
public class HistoryPersistenceServiceTests : IDisposable
{
    private readonly string testPath;
    private readonly HistoryPersistenceService service;
    private readonly TransactionHistory history;

    /// <summary>HistoryPersistenceServiceTests の新しいインスタンスを初期化します。</summary>
    public HistoryPersistenceServiceTests()
    {
        testPath = Path.Combine(Path.GetTempPath(), $"history_test_{Guid.NewGuid()}.bin");
        history = new TransactionHistory();
        service = new HistoryPersistenceService(history, testPath);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        service.Dispose();
        if (File.Exists(testPath))
        {
            File.Delete(testPath);
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>ファイルが存在しない場合に Load が空の履歴を返すことを検証します。</summary>
    [Fact]
    public void LoadShouldReturnEmptyWhenFileDoesNotExist()
    {
        // Act
        var state = service.Load();

        // Assert
        state.ShouldNotBeNull();
        state.Entries.ShouldBeEmpty();
    }

    /// <summary>Save によりファイルが作成されることを検証します。</summary>
    [Fact]
    public void SaveShouldCreateFile()
    {
        // Arrange
        var state = new HistoryState
        {
            Entries = new List<TransactionEntry>
            {
                new(DateTimeOffset.Now, TransactionType.Deposit, 100, new Dictionary<DenominationKey, int>())
            }
        };

        // Act
        service.Save(state);

        // Assert
        File.Exists(testPath).ShouldBeTrue();
    }

    /// <summary>オートセーブが有効な際、履歴の追加に伴い自動的に保存が行われることを検証します。</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task AutoSaveShouldTriggerWhenEntryAdded()
    {
        // Arrange
        service.StartAutoSave();
        var entry = new TransactionEntry(DateTimeOffset.Now, TransactionType.Deposit, 500, new Dictionary<DenominationKey, int>());

        // Act
        history.Add(entry);

        // Give it a moment to save
        await Task.Delay(200, TestContext.Current.CancellationToken).ConfigureAwait(false);

        // Assert
        File.Exists(testPath).ShouldBeTrue();
        var loaded = service.Load();
        loaded.Entries.Count.ShouldBeGreaterThan(0);
        loaded.Entries[0].Amount.ShouldBe(500);
    }

    /// <summary>ファイルが破損している場合に Load が空の履歴を返すことを検証します。</summary>
    [Fact]
    public void LoadShouldReturnEmptyWhenFileIsCorrupted()
    {
        // Arrange
        File.WriteAllText(testPath, "INVALID_BINARY_DATA");

        // Act
        var state = service.Load();

        // Assert
        state.ShouldNotBeNull();
        state.Entries.ShouldBeEmpty();
    }

    /// <summary>Save メソッドに null を渡した場合に ArgumentNullException がスローされることを検証します。</summary>
    [Fact]
    public void SaveShouldThrowWhenStateIsNull()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => service.Save(null!));
    }

    /// <summary>読み込み時に IOException が発生した場合に空の履歴を返すことを検証します。</summary>
    [Fact]
    public void LoadShouldHandleIOException()
    {
        // Arrange
        File.WriteAllBytes(testPath, [12, 34]);
        using var fs = new FileStream(testPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        // Act
        var state = service.Load();

        // Assert
        state.ShouldNotBeNull();
        state.Entries.ShouldBeEmpty();
    }

    /// <summary>保存時に IOException が発生しても例外が伝播せず適切にハンドルされることを検証します。</summary>
    [Fact]
    public void SaveShouldHandleIOException()
    {
        // Arrange
        File.WriteAllBytes(testPath, [12, 34]);
        using var fs = new FileStream(testPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        // Act & Assert
        var state = new HistoryState { Entries = [] };
        Should.NotThrow(() => service.Save(state));
    }

    /// <summary>アクセス権限がない場合に Load が適切にハンドルし空の履歴を返すことを検証します。</summary>
    [Fact]
    public void LoadShouldHandleUnauthorizedAccessException()
    {
        // Arrange
        var dirPath = Path.Combine(Path.GetTempPath(), $"dir_{Guid.NewGuid()}");
        Directory.CreateDirectory(dirPath);
        var serviceWithDir = new HistoryPersistenceService(history, dirPath);

        try
        {
            // Act
            var state = serviceWithDir.Load();

            // Assert
            state.ShouldNotBeNull();
            state.Entries.ShouldBeEmpty();
        }
        finally
        {
            Directory.Delete(dirPath);
        }
    }

    /// <summary>アクセス権限がない場合に Save が例外をスローせず適切にハンドルすることを検証します。</summary>
    [Fact]
    public void SaveShouldHandleUnauthorizedAccessException()
    {
        // Arrange
        var dirPath = Path.Combine(Path.GetTempPath(), $"dir_save_{Guid.NewGuid()}");
        Directory.CreateDirectory(dirPath);
        var serviceWithDir = new HistoryPersistenceService(history, dirPath);

        try
        {
            // Act & Assert
            var state = new HistoryState { Entries = [] };
            Should.NotThrow(() => serviceWithDir.Save(state));
        }
        finally
        {
            Directory.Delete(dirPath);
        }
    }

    /// <summary>Dispose メソッドが複数回呼び出されても安全であることを検証します。</summary>
    [Fact]
    public void DisposeShouldBeIdempotent()
    {
        // Act & Assert
        Should.NotThrow(() =>
        {
            service.Dispose();
            service.Dispose();
        });
    }
}
