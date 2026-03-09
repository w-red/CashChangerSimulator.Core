using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Core.Configuration;
using Shouldly;
using System.IO;

namespace CashChangerSimulator.Tests.Transactions;

public class HistoryPersistenceServiceTests : IDisposable
{
    private readonly string _testPath;
    private readonly HistoryPersistenceService _service;
    private readonly TransactionHistory _history;

    public HistoryPersistenceServiceTests()
    {
        _testPath = Path.Combine(Path.GetTempPath(), $"history_test_{Guid.NewGuid()}.bin");
        _history = new TransactionHistory();
        _service = new HistoryPersistenceService(_history, _testPath);
    }

    [Fact]
    public void Load_ShouldReturnEmpty_WhenFileDoesNotExist()
    {
        // Arrange
        if (File.Exists(_testPath)) File.Delete(_testPath);

        // Act
        var state = _service.Load();

        // Assert
        state.ShouldNotBeNull();
        state.Entries.ShouldBeEmpty();
    }

    [Fact]
    public void Save_ShouldCreateFile()
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
        _service.Save(state);

        // Assert
        File.Exists(_testPath).ShouldBeTrue();
    }

    [Fact]
    public async Task AutoSave_ShouldTriggerWhenEntryAdded()
    {
        // Arrange
        _service.StartAutoSave();
        var entry = new TransactionEntry(DateTimeOffset.Now, TransactionType.Deposit, 500, new Dictionary<DenominationKey, int>());

        // Act
        _history.Add(entry);
        
        // Give it a moment to save
        await Task.Delay(200, TestContext.Current.CancellationToken);

        // Assert
        File.Exists(_testPath).ShouldBeTrue();
        var loaded = _service.Load();
        loaded.Entries.Count.ShouldBeGreaterThan(0);
        loaded.Entries[0].Amount.ShouldBe(500);
    }

    public void Dispose()
    {
        _service.Dispose();
        if (File.Exists(_testPath)) File.Delete(_testPath);
    }
}
