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

    /// <summary>Initializes a new instance of the <see cref="HistoryPersistenceServiceTests"/> class.HistoryPersistenceServiceTests の新しいインスタンスを初期化します。</summary>
    public HistoryPersistenceServiceTests()
    {
        testPath = Path.Combine(Path.GetTempPath(), $"history_test_{Guid.NewGuid()}.bin");
        history = new TransactionHistory();
        service = new HistoryPersistenceService(history, testPath);
    }

    /// <summary>ファイルが存在しない場合に Load が空の履歴を返すことを検証します。</summary>
    [Fact]
    public void LoadShouldReturnEmptyWhenFileDoesNotExist()
    {
        // Arrange
        if (File.Exists(testPath))
        {
            File.Delete(testPath);
        }

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
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous unit test.</placeholder></returns>
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

    /// <summary>保存先パスが不正な場合でも例外が伝播せず適切にハンドルされることを検証します。</summary>
    [Fact]
    public void SaveShouldHandleExceptionWhenPathIsInvalid()
    {
        // Arrange
        var invalidPath = Path.Combine(testPath, "invalid_subdir", "file.bin"); // Path to a non-existent directory
        var service = new HistoryPersistenceService(history, invalidPath);
        var state = new HistoryState { Entries = [] };

        // Act & Assert
        Should.NotThrow(() => service.Save(state));
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

    /// <summary>TransactionEntry が MemoryPack によりシリアライズ・デシリアライズ可能であることを検証します。</summary>
    [Fact]
    public void TransactionEntryCanBeMemoryPackDeserialized()
    {
        // Arrange
        var dict = new Dictionary<DenominationKey, int>
        {
            { new DenominationKey(1000, CurrencyCashType.Bill), 2 }
        };
        var original = new TransactionEntry(DateTimeOffset.Now, TransactionType.Deposit, 2000, dict);

        // Act
        var bin = MemoryPack.MemoryPackSerializer.Serialize(original);
        var restored = MemoryPack.MemoryPackSerializer.Deserialize<TransactionEntry>(bin);

        // Assert
        restored.ShouldNotBeNull();
        restored.Amount.ShouldBe(2000);
        restored.Type.ShouldBe(TransactionType.Deposit);
        restored.Timestamp.ShouldBe(original.Timestamp);
        restored.Counts.Count.ShouldBe(1);
    }
}
