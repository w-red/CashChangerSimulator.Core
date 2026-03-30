using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Transactions;
using Shouldly;

namespace CashChangerSimulator.Tests.Transactions;

/// <summary>取引履歴の件数制限や永続化の整合性を検証するテストクラス。</summary>
public class HistoryPersistenceTests
{
    /// <summary>設定ファイルで指定された最大履歴件数が正しく適用されることを検証します。</summary>
    [Fact]
    public void TransactionHistoryShouldRespectMaxEntriesFromConfig()
    {
        // Arrange
        var config = new SimulatorConfiguration();
        config.History.MaxEntries = 3;
        var history = new TransactionHistory(config);

        // Act
        history.Add(new TransactionEntry(DateTimeOffset.Now, TransactionType.Deposit, 100, new Dictionary<DenominationKey, int>()));
        history.Add(new TransactionEntry(DateTimeOffset.Now, TransactionType.Deposit, 200, new Dictionary<DenominationKey, int>()));
        history.Add(new TransactionEntry(DateTimeOffset.Now, TransactionType.Deposit, 300, new Dictionary<DenominationKey, int>()));
        history.Add(new TransactionEntry(DateTimeOffset.Now, TransactionType.Deposit, 400, new Dictionary<DenominationKey, int>()));

        // Assert
        history.Entries.Count.ShouldBe(3);
        history.Entries[0].Amount.ShouldBe(400);
        history.Entries[2].Amount.ShouldBe(200);
    }

    /// <summary>外部状態（HistoryState）からの復元時にも最大履歴件数が維持されることを検証します。</summary>
    [Fact]
    public void FromStateShouldRespectMaxEntriesFromConfig()
    {
        // Arrange
        var config = new SimulatorConfiguration();
        config.History.MaxEntries = 2;
        var history = new TransactionHistory(config);
        var state = new HistoryState
        {
            Entries =
            [
                new TransactionEntry(DateTimeOffset.Now, TransactionType.Deposit, 100, new Dictionary<DenominationKey, int>()),
                new TransactionEntry(DateTimeOffset.Now, TransactionType.Deposit, 200, new Dictionary<DenominationKey, int>()),
                new TransactionEntry(DateTimeOffset.Now, TransactionType.Deposit, 300, new Dictionary<DenominationKey, int>())
            ]
        };

        // Act
        history.FromState(state);

        // Assert
        history.Entries.Count.ShouldBe(2);
        history.Entries[0].Amount.ShouldBe(100);
        history.Entries[1].Amount.ShouldBe(200);
    }
}
