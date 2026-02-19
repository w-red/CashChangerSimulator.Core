namespace CashChangerSimulator.Tests.Core;

using CashChangerSimulator.Core.Models;
using MoneyKind4Opos.Currencies.Interfaces;
using R3;
using Shouldly;
using System.Linq;
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
        
        CashStatus deviceStatus = CashStatus.Unknown;
        CashStatus fullStatus = CashStatus.Unknown;
        using var _d = changerStatus.DeviceStatus.Subscribe(s => deviceStatus = s);
        using var _f = changerStatus.FullStatus.Subscribe(s => fullStatus = s);

        // Assert: 初期状態 (両方 0 枚) -> DeviceStatus=Empty, FullStatus=Normal
        deviceStatus.ShouldBe(CashStatus.Empty);
        fullStatus.ShouldBe(CashStatus.Normal);

        // Act: 1000円を Normal にする (1000: 5枚, 5000: 0枚)
        inventory.SetCount(denominations[0], 5);
        // Assert: 5000円がまだ Empty なので、DeviceStatus=Empty
        deviceStatus.ShouldBe(CashStatus.Empty);

        // Act: 5000円も Normal にする (1000: 5枚, 5000: 5枚)
        inventory.SetCount(denominations[1], 5);
        // Assert: 両方正常
        deviceStatus.ShouldBe(CashStatus.Normal);
        fullStatus.ShouldBe(CashStatus.Normal);

        // Act: 1000円を Full にする (1000: 10枚, 5000: 5枚)
        inventory.SetCount(denominations[0], 10);
        // Assert: FullStatus=Full
        fullStatus.ShouldBe(CashStatus.Full);
        deviceStatus.ShouldBe(CashStatus.Normal);
        
        // Act: 1000円を Full のまま、5000円を Empty にする (1000: 10枚, 5000: 0枚)
        inventory.SetCount(denominations[1], 0);
        // Assert: 両方の異常が独立して報告される
        fullStatus.ShouldBe(CashStatus.Full);
        deviceStatus.ShouldBe(CashStatus.Empty);

        // Act: 1000円を Normal に戻し、5000円を NearEmpty にする (1000: 5枚, 5000: 1枚)
        inventory.SetCount(denominations[0], 5);
        inventory.SetCount(denominations[1], 1);
        // Assert: NearEmpty
        deviceStatus.ShouldBe(CashStatus.NearEmpty);
        fullStatus.ShouldBe(CashStatus.Normal);
    }
}
