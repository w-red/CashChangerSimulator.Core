using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Device.PosForDotNet;
using CashChangerSimulator.Device.PosForDotNet.Models;
using CashChangerSimulator.Device.Virtual;
using Microsoft.PointOfService;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>OPOS/UPOS 規格への準拠性とシミュレータ固有の拡張機能を検証するテストクラス。</summary>
/// <remarks>
/// リアルタイムデータ通知、ディレクトIOによる状態操作、不整合フラグのレポートなど、
/// デバイスの標準的な振る舞いとカスタムコマンドの正確性を網羅的に検証します。
/// </remarks>
[Collection("GlobalLock")]
public class ComplianceTests
{
    private static (InternalSimulatorCashChanger changer, DepositController controller, Inventory inventory, CashChangerSimulator.Core.Transactions.TransactionHistory history, DeviceEventHistoryObserver observer) CreateChanger()
    {
        var inventory = Inventory.Create();
        var hardwareStatusManager = HardwareStatusManager.Create();
        hardwareStatusManager.Input.IsConnected.Value = true;
        var history = new CashChangerSimulator.Core.Transactions.TransactionHistory();
        var controller = new DepositController(inventory, hardwareStatusManager);
        var deps = new SimulatorDependencies(
                null,
                inventory,
                history,
                null,
                controller,
                null,
                null,
                hardwareStatusManager);
        var changer =
            new InternalSimulatorCashChanger(deps)
            {
                DisableUposEventQueuing = true, // Avoid NRE in POS.NET internals
                SkipStateVerification = true
            };
        changer.Open();
        changer.Claim(0);
        changer.DeviceEnabled = true;
        changer.DataEventEnabled = true;

        var observer = new DeviceEventHistoryObserver(changer, history);

        return (changer, controller, inventory, history, observer);
    }

    /// <summary>ReadCashCounts が在庫の不整合（Discrepancy）を正しく報告することを検証します。</summary>
    [Fact]
    public void ReadCashCountsShouldReportDiscrepancy()
    {
        // Arrange
        var (changer, _, inventory, _, _) = CreateChanger();
        inventory.HasDiscrepancy = true;

        // Act
        var counts = changer.ReadCashCounts();

        // Assert
        counts.Discrepancy.ShouldBeTrue();
    }

    /// <summary>リアルタイム通知が無効な場合、入金確定時にのみ DataEvent が発火することを検証します。</summary>
    [Fact]
    public async Task RealTimeDataEnabledFalseShouldFireDataEventOnlyOnFix()
    {
        // Arrange
        var (changer, controller, _, _, _) = CreateChanger();
        changer.RealTimeDataEnabled = false;
        int eventCount = 0;
        changer.OnEventQueued +=
            (e) =>
            {
                if (e is DataEventArgs)
                {
                    eventCount++;
                }
            };

        // Act
        changer.BeginDeposit();
        controller.TrackDeposit(new DenominationKey(1000, CurrencyCashType.Bill, "JPY"));
        eventCount.ShouldBe(0); // Not fired yet

        changer.FixDeposit();
        await WaitUntil(() => eventCount == 1);

        eventCount.ShouldBe(1); // Fired on Fix (buffered data notification)
    }

    /// <summary>リアルタイム通知が有効な場合、投入の都度 DataEvent が発火することを検証します。</summary>
    [Fact]
    public async Task RealTimeDataEnabledTrueShouldFireDataEventOnTrack()
    {
        // Arrange
        var (changer, controller, _, history, _) = CreateChanger();
        changer.RealTimeDataEnabled = true;
        changer.BeginDeposit();

        int eventCount = 0;
        changer.OnEventQueued += (e) =>
        {
            if (e is DataEventArgs)
            {
                eventCount++;
            }
        };

        // Act
        controller.TrackDeposit(new DenominationKey(1000, CurrencyCashType.Bill, "JPY"));
        await WaitUntil(() => eventCount == 1);

        // Assert
        eventCount.ShouldBe(1); // Fired immediately
        history.Entries.ShouldContain(e => e.Type == CashChangerSimulator.Core.Transactions.TransactionType.DataEvent);
    }

    /// <summary>DirectIO(SimulateRemoved) によりカセット取外しイベントが発火することを検証します。</summary>
    [Fact]
    public async Task DirectIOSimulateRemovedShouldFireStatusUpdateEvent()
    {
        // Arrange
        var (changer, _, _, _, _) = CreateChanger();
        int status = -1;
        changer
            .OnEventQueued
            += (e) =>
            {
                if (e is StatusUpdateEventArgs se)
                {
                    status = se.Status;
                }
            };

        // Act
        changer.DirectIO(DirectIOCommands.SimulateRemoved, 0, string.Empty);
        await WaitUntil(() => status == 41);

        // Assert
        status.ShouldBe(41); // CHAN_STATUS_REMOVED
    }

    /// <summary>DirectIO(SimulateInserted) によりカセット装着イベントが発火することを検証します。</summary>
    [Fact]
    public async Task DirectIOSimulateInsertedShouldFireStatusUpdateEvent()
    {
        // Arrange
        var (changer, _, _, _, _) = CreateChanger();
        int status = -1;
        changer.OnEventQueued += (e) =>
        {
            if (e is StatusUpdateEventArgs se)
            {
                status = se.Status;
            }
        };

        // Act
        changer.DirectIO(DirectIOCommands.SimulateInserted, 0, string.Empty);
        await WaitUntil(() => status == 42);

        // Assert
        status.ShouldBe(42); // CHAN_STATUS_INSERTED
    }

    /// <summary>DirectIO(SetDiscrepancy) により不整合フラグが更新されることを検証します。</summary>
    [Fact]
    public void DirectIOSetDiscrepancyShouldUpdateHasDiscrepancy()
    {
        // Arrange
        var (changer, _, _, _, _) = CreateChanger();
        changer.ReadCashCounts().Discrepancy.ShouldBeFalse();

        // Act & Assert (Enable)
        changer.DirectIO(DirectIOCommands.SetDiscrepancy, 1, string.Empty);
        changer.ReadCashCounts().Discrepancy.ShouldBeTrue();

        // Act & Assert (Disable)
        changer.DirectIO(DirectIOCommands.SetDiscrepancy, 0, string.Empty);
        changer.ReadCashCounts().Discrepancy.ShouldBeFalse();
    }

    /// <summary>DirectIO(AdjustCashCountsStr) により在庫が文字列指定で更新されることを検証します。</summary>
    [Fact]
    public void DirectIOAdjustCashCountsStrShouldUpdateInventory()
    {
        // Arrange
        var (changer, _, inventory, _, _) = CreateChanger();
        var jpy1000 = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");

        // Seed the inventory first so the parser knows this key exists
        inventory.SetCount(jpy1000, 0);

        inventory.GetCount(jpy1000).ShouldBe(0);

        // Act
        changer.DirectIO(DirectIOCommands.AdjustCashCountsStr, 0, "1000:15");

        // Assert
        inventory.GetCount(jpy1000).ShouldBe(15);
    }

    /// <summary>AdjustCashCounts(string) により在庫が正しく更新されることを検証します。</summary>
    [Fact]
    public void AdjustCashCountsStrShouldUpdateInventory()
    {
        // Arrange
        var (changer, _, inventory, _, _) = CreateChanger();
        var jpy1000 = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        inventory.SetCount(jpy1000, 0);

        // Act
        changer.AdjustCashCounts("1000:20");

        // Assert
        inventory.GetCount(jpy1000).ShouldBe(20);
    }

    /// <summary>ReadCashCounts(ref variables) 形式で不整合フラグが正しく取得できることを検証します。</summary>
    [Fact]
    public void ReadCashCountsWithDiscrepancyShouldReturnProperFlags()
    {
        // Arrange
        var (changer, _, inventory, _, _) = CreateChanger();
        inventory.HasDiscrepancy = true;
        string counts = string.Empty;
        bool discrepancy = false;

        // Act
        changer.ReadCashCounts(ref counts, ref discrepancy);

        // Assert
        discrepancy.ShouldBeTrue();
    }

    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 2000)
    {
        var start = DateTime.Now;
        while (!condition() && (DateTime.Now - start).TotalMilliseconds < timeoutMs)
        {
            await Task.Delay(10);
        }
    }
}
