using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using MoneyKind4Opos.Currencies.Interfaces;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>SimulatorCashChanger の StatusUpdateEvent 発火を検証するテストクラス (TDD Red)。</summary>
public class StatusUpdateEventTests
{
    private static readonly DenominationKey Coin100 = new(100, CashType.Coin, "JPY");

    /// <summary>テスト用の SimulatorCashChanger を生成する。</summary>
    private static (SimulatorCashChanger cc, Inventory inv, HardwareStatusManager hw, List<int> events) CreateTestCashChanger(
        int nearEmpty = 5, int nearFull = 90, int full = 100)
    {
        var config = new SimulatorConfiguration();
        config.Thresholds.NearEmpty = nearEmpty;
        config.Thresholds.NearFull = nearFull;
        config.Thresholds.Full = full;

        // Initialize with a denomination
        config.Inventory["JPY"] = new InventorySettings
        {
            Denominations = new()
            {
                ["C100"] = new() { InitialCount = 50, NearEmpty = nearEmpty, NearFull = nearFull, Full = full }
            }
        };

        var inv = new Inventory();
        inv.SetCount(Coin100, 50); // Start at normal level

        var hw = new HardwareStatusManager();
        var history = new TransactionHistory();
        var manager = new CashChangerManager(inv, history);

        var cc = new SimulatorCashChanger(config, inv, history, manager, null, null, null, hw);

        var events = new List<int>();
        cc.OnEventQueued += e =>
        {
            if (e is StatusUpdateEventArgs sue)
            {
                events.Add(sue.Status);
            }
        };

        return (cc, inv, hw, events);
    }

    // ========== DeviceStatus StatusUpdateEvent ==========

    [Fact]
    public void ShouldFireStatusUpdateEvent_WhenDeviceStatus_BecomesEmpty()
    {
        var (cc, inv, hw, events) = CreateTestCashChanger();

        // Reduce count to 0 → should trigger Empty status update
        inv.SetCount(Coin100, 0);

        // Allow reactive pipeline to propagate
        Thread.Sleep(50);

        events.ShouldContain((int)UposCashChangerStatusUpdateCode.Empty);
    }

    [Fact]
    public void ShouldFireStatusUpdateEvent_WhenDeviceStatus_BecomesNearEmpty()
    {
        var (cc, inv, hw, events) = CreateTestCashChanger();

        // Reduce count to NearEmpty threshold
        inv.SetCount(Coin100, 3);

        Thread.Sleep(50);

        events.ShouldContain((int)UposCashChangerStatusUpdateCode.NearEmpty);
    }

    [Fact]
    public void ShouldFireStatusUpdateEvent_WhenDeviceStatus_RecoversFromEmpty()
    {
        var (cc, inv, hw, events) = CreateTestCashChanger();

        // Go to Empty
        inv.SetCount(Coin100, 0);
        Thread.Sleep(50);
        events.Clear();

        // Recover
        inv.SetCount(Coin100, 50);
        Thread.Sleep(50);

        events.ShouldContain((int)UposCashChangerStatusUpdateCode.EmptyOk);
    }

    // ========== FullStatus OK transitions ==========

    [Fact]
    public void ShouldFireStatusUpdateEvent_WhenFullStatus_RecoversFromFull()
    {
        var (cc, inv, hw, events) = CreateTestCashChanger();

        // Go to Full
        inv.SetCount(Coin100, 100);
        Thread.Sleep(50);
        events.Clear();

        // Recover
        inv.SetCount(Coin100, 50);
        Thread.Sleep(50);

        events.ShouldContain((int)UposCashChangerStatusUpdateCode.FullOk);
    }

    // ========== Jam StatusUpdateEvent ==========

    [Fact]
    public void ShouldFireStatusUpdateEvent_WithStandardJamCode_WhenJammed()
    {
        var (cc, inv, hw, events) = CreateTestCashChanger();

        hw.SetJammed(true);
        Thread.Sleep(50);

        // UPOS standard: CHAN_STATUS_JAM = 31
        events.ShouldContain((int)UposCashChangerStatusUpdateCode.Jam);
    }

    [Fact]
    public void ShouldFireStatusUpdateEvent_WithOkCode_WhenJamCleared()
    {
        var (cc, inv, hw, events) = CreateTestCashChanger();

        hw.SetJammed(true);
        Thread.Sleep(50);
        events.Clear();

        hw.SetJammed(false);
        Thread.Sleep(50);

        // UPOS standard: CHAN_STATUS_OK = 0
        events.ShouldContain((int)UposCashChangerStatusUpdateCode.Ok);
    }
}
