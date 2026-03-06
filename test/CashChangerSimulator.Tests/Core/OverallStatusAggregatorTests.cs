using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using R3;
using Shouldly;

namespace CashChangerSimulator.Tests.Core;

/// <summary>OverallStatusAggregator の集約ロジックを検証するテスト。</summary>
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
            new DenominationKey(1000, CurrencyCashType.Bill),
            new DenominationKey(5000, CurrencyCashType.Bill)
        };

        // モニター作成
        var monitors = denominations.Select(d =>
            new CashStatusMonitor(inventory, d, nearEmptyThreshold: 2, nearFullThreshold: 8, fullThreshold: 10)
        ).ToList();

        var aggregator = new OverallStatusAggregator(monitors);

        CashStatus deviceStatus = CashStatus.Unknown;
        CashStatus fullStatus = CashStatus.Unknown;
        using var _d = aggregator.DeviceStatus.Subscribe(s => deviceStatus = s);
        using var _f = aggregator.FullStatus.Subscribe(s => fullStatus = s);

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
    }

    /// <summary>多数のモニターを扱う際に正しく集約されることを検証する。</summary>
    [Fact]
    public void AggregatorShouldHandleManyMonitors()
    {
        // Arrange
        var inventory = new Inventory();
        var monitors = new List<CashStatusMonitor>();
        for (int i = 1; i <= 5; i++)
        {
            var key = new DenominationKey(i * 1000, CurrencyCashType.Bill);
            inventory.SetCount(key, 5); // All Normal
            monitors.Add(new CashStatusMonitor(inventory, key, 2, 8, 10));
        }

        var aggregator = new OverallStatusAggregator(monitors);
        aggregator.DeviceStatus.CurrentValue.ShouldBe(CashStatus.Normal);

        // Act: One becomes NearEmpty
        inventory.SetCount(monitors[2].Key, 1);

        // Assert
        aggregator.DeviceStatus.CurrentValue.ShouldBe(CashStatus.NearEmpty);

        // Act: Another becomes Empty
        inventory.SetCount(monitors[4].Key, 0);

        // Assert
        aggregator.DeviceStatus.CurrentValue.ShouldBe(CashStatus.Empty);
    }

    /// <summary>Dispose 呼び出しによりリソースが解放されることを検証する（カバレッジ用）。</summary>
    [Fact]
    public void DisposeShouldWork()
    {
        // Arrange
        var aggregator = new OverallStatusAggregator([]);

        // Act & Assert
        Should.NotThrow(() => aggregator.Dispose());
    }

    /// <summary>Refresh 呼び出しにより監視対象が更新され、集約結果が再計算されることを検証する。</summary>
    [Fact]
    public void RefreshShouldUpdateMonitorsAndRecalculate()
    {
        // Arrange
        var inventory = new Inventory();
        var k1000 = new DenominationKey(1000, CurrencyCashType.Bill);
        var k5000 = new DenominationKey(5000, CurrencyCashType.Bill);
        
        inventory.SetCount(k1000, 5); // Normal
        inventory.SetCount(k5000, 0); // Empty
        
        var monitor1000 = new CashStatusMonitor(inventory, k1000, 2, 8, 10);
        var aggregator = new OverallStatusAggregator([monitor1000]);
        
        // 初期状態: 1000円しか監視していないので Normal
        aggregator.DeviceStatus.CurrentValue.ShouldBe(CashStatus.Normal);
        
        // Act: 5000円のモニターを追加して Refresh
        var monitor5000 = new CashStatusMonitor(inventory, k5000, 2, 8, 10);
        aggregator.Refresh([monitor1000, monitor5000]);
        
        // Assert: 5000円が Empty なので集約結果も Empty になるはず
        aggregator.DeviceStatus.CurrentValue.ShouldBe(CashStatus.Empty);
    }
}
