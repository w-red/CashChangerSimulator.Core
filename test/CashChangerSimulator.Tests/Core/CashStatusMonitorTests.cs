namespace CashChangerSimulator.Tests.Core;

using CashChangerSimulator.Core.Models;
using MoneyKind4Opos.Currencies.Interfaces;
using R3;
using Shouldly;
using Xunit;

/// <summary>
/// CashStatusMonitor の状態遷移を検証するテスト。
/// </summary>
public class CashStatusMonitorTests
{
    /// <summary>在庫の枚数に応じてステータスが正しく遷移することを検証する。</summary>
    [Fact]
    public void MonitorShouldTransitionStatusBasedOnInventory()
    {
        // Arrange
        var inventory = new Inventory();
        var denomination = new DenominationKey(1000, CashType.Bill);
        
        // しきい値設定: Empty=0, NearEmpty=2, NearFull=8, Full=10
        // 0: Empty
        // 1: NearEmpty
        // 2-7: Normal
        // 8-9: NearFull
        // 10+: Full
        var monitor = new CashStatusMonitor(inventory, denomination, nearEmptyThreshold: 2, nearFullThreshold: 8, fullThreshold: 10);
        
        CashStatus currentStatus = CashStatus.Unknown;
        using var _ = monitor.Status.Subscribe(s => currentStatus = s);

        // Assert: Initial (0)
        currentStatus.ShouldBe(CashStatus.Empty);

        // Act & Assert: Add to 1 (NearEmpty)
        inventory.Add(denomination, 1);
        currentStatus.ShouldBe(CashStatus.NearEmpty);

        // Act & Assert: Add to 5 (Normal)
        inventory.Add(denomination, 4);
        currentStatus.ShouldBe(CashStatus.Normal);

        // Act & Assert: Add to 9 (NearFull)
        inventory.Add(denomination, 4);
        currentStatus.ShouldBe(CashStatus.NearFull);

        // Act & Assert: Add to 10 (Full)
        inventory.Add(denomination, 1);
        currentStatus.ShouldBe(CashStatus.Full);
        
        // Act & Assert: Add more (Still Full)
        inventory.Add(denomination, 5);
        currentStatus.ShouldBe(CashStatus.Full);
    }
}
