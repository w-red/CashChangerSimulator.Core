using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using MoneyKind4Opos.Currencies.Interfaces;
using R3;
using Shouldly;

namespace CashChangerSimulator.Tests.Core;
/// <summary>CashStatusMonitor の状態遷移を検証するテスト。</summary>
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
        var monitor = new CashStatusMonitor(inventory, denomination, nearEmptyThreshold: 2, nearFullThreshold: 8, fullThreshold: 10);

        CashStatus currentStatus = CashStatus.Unknown;
        using var _ = monitor.Status.Subscribe(s => currentStatus = s);

        // Assert: Initial (0)
        currentStatus.ShouldBe(CashStatus.Empty);

        // Act & Assert: Add to 1 (NearEmpty)
        inventory.Add(denomination, 1);
        currentStatus.ShouldBe(CashStatus.NearEmpty);

        // Act & Assert: Add to 2 (Normal)
        inventory.Add(denomination, 1);
        currentStatus.ShouldBe(CashStatus.Normal);

        // Act & Assert: Add to 8 (NearFull)
        inventory.Add(denomination, 6);
        currentStatus.ShouldBe(CashStatus.NearFull);

        // Act & Assert: Add to 10 (Full)
        inventory.Add(denomination, 2);
        currentStatus.ShouldBe(CashStatus.Full);
    }

    /// <summary>しきい値を動的に更新した際、ステータスが再計算されることを検証する。</summary>
    [Fact]
    public void UpdateThresholdsShouldRecalculateStatus()
    {
        // Arrange
        var inventory = new Inventory();
        var denomination = new DenominationKey(1000, CashType.Bill);
        inventory.SetCount(denomination, 5); // 5枚

        // 初期しきい値では Normal (NearFull=8)
        var monitor = new CashStatusMonitor(inventory, denomination, nearEmptyThreshold: 2, nearFullThreshold: 8, fullThreshold: 10);
        monitor.Status.CurrentValue.ShouldBe(CashStatus.Normal);

        // Act: 近満杯しきい値を5以下に下げる
        monitor.UpdateThresholds(nearEmpty: 2, nearFull: 5, full: 10);

        // Assert
        monitor.Status.CurrentValue.ShouldBe(CashStatus.NearFull);

        // Act: 満杯しきい値を5にする
        monitor.UpdateThresholds(nearEmpty: 2, nearFull: 3, full: 5);
        monitor.Status.CurrentValue.ShouldBe(CashStatus.Full);
    }

    /// <summary>Dispose 呼び出しにより購読が解除されることを検証する。</summary>
    [Fact]
    public void DisposeShouldUnsubscribe()
    {
        // Arrange
        var inventory = new Inventory();
        var denomination = new DenominationKey(1000, CashType.Bill);
        var monitor = new CashStatusMonitor(inventory, denomination, nearEmptyThreshold: 2, nearFullThreshold: 8, fullThreshold: 10);

        monitor.Status.CurrentValue.ShouldBe(CashStatus.Empty);

        // Act
        monitor.Dispose();

        // 在庫を増やしてもステータスが変わらないことを確認
        inventory.SetCount(denomination, 5);
        
        // Assert
        // 注: Dispose しても既存の ReactiveProperty の値は残るが、更新はされないはず
        monitor.Status.CurrentValue.ShouldBe(CashStatus.Empty);
    }

    /// <summary>しきい値に -1 を設定した場合、そのステータス判定をスキップすることを検証する。</summary>
    [Fact]
    public void ThresholdOfMinusOneShouldDisableStatusCheck()
    {
        // Arrange
        var inventory = new Inventory();
        var denomination = new DenominationKey(1000, CashType.Bill);

        // すべてのしきい値を -1 (無効) に設定
        var monitor = new CashStatusMonitor(inventory, denomination, nearEmptyThreshold: -1, nearFullThreshold: -1, fullThreshold: -1);

        // Assert: 0枚でも Empty にならない (NearEmpty=-1 のため)
        inventory.SetCount(denomination, 0);
        monitor.Status.CurrentValue.ShouldBe(CashStatus.Normal);

        // Act & Assert: 大量に在庫を増やしても Normal のまま
        inventory.SetCount(denomination, 100);
        monitor.Status.CurrentValue.ShouldBe(CashStatus.Normal);

        // Act: NearFull だけ有効にする
        monitor.UpdateThresholds(nearEmpty: -1, nearFull: 50, full: -1);
        inventory.SetCount(denomination, 49);
        monitor.Status.CurrentValue.ShouldBe(CashStatus.Normal);
        inventory.SetCount(denomination, 50);
        monitor.Status.CurrentValue.ShouldBe(CashStatus.NearFull);

        // Full は -1 なので 満杯にはならない
        inventory.SetCount(denomination, 100);
        monitor.Status.CurrentValue.ShouldBe(CashStatus.NearFull);
    }
}
