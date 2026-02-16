namespace CashChangerSimulator.Tests.Core;

using System.Linq;
using CashChangerSimulator.Core.Models;
using MoneyKind4Opos.Currencies.Interfaces;
using R3;
using Shouldly;
using Xunit;

/// <summary>
/// OverallStatusAggregator の集約ロジックを検証するテスト。
/// </summary>
public class OverallStatusAggregatorTests
{
    /// <summary>各金種のステータスに基づき、全体のステータスが正しく集約されることを検証する。</summary>
    [Fact]
    public void OverallStatusAggregatorShouldAggregateIndividualStatuses()
    {
        // Arrange
        var inventory = new Inventory();
        var denominations = new[] 
        { 
            new DenominationKey(1000, CashType.Bill),
            new DenominationKey(5000, CashType.Bill)
        };
        
        // モニター作成
        var monitors = denominations.Select(d => 
            new CashStatusMonitor(inventory, d, nearEmptyThreshold: 2, nearFullThreshold: 8, fullThreshold: 10)
        ).ToList();

        var changerStatus = new OverallStatusAggregator(monitors);
        
        CashStatus currentOverall = CashStatus.Unknown;
        using var _ = changerStatus.OverallStatus.Subscribe(s => currentOverall = s);

        // Assert: 初期状態 (両方 0 枚) -> Empty
        currentOverall.ShouldBe(CashStatus.Empty);

        // Act: 1000円を Normal にする (1000: 5枚, 5000: 0枚)
        inventory.SetCount(denominations[0], 5);
        // Assert: 5000円がまだ Empty なので、全体としては Empty
        currentOverall.ShouldBe(CashStatus.Empty);

        // Act: 5000円も Normal にする (1000: 5枚, 5000: 5枚)
        inventory.SetCount(denominations[1], 5);
        // Assert: 全て正常なので Normal
        currentOverall.ShouldBe(CashStatus.Normal);

        // Act: 1000円を Full にする (1000: 10枚, 5000: 5枚)
        inventory.SetCount(denominations[0], 10);
        // Assert: 一方の金種が Full なので全体も Full
        currentOverall.ShouldBe(CashStatus.Full);
        
        // Act: 1000円を Normal に戻し、5000円を NearEmpty にする (1000: 5枚, 5000: 1枚)
        inventory.SetCount(denominations[0], 5);
        inventory.SetCount(denominations[1], 1);
        // Assert: 5000円が NearEmpty なので全体も NearEmpty
        currentOverall.ShouldBe(CashStatus.NearEmpty);
    }
}
