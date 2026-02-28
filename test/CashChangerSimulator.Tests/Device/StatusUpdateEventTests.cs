using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using MoneyKind4Opos.Currencies.Interfaces;
using Moq;
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
        var configProvider = new ConfigurationProvider();
        configProvider.Config.Thresholds.NearEmpty = nearEmpty;
        configProvider.Config.Thresholds.NearFull = nearFull;
        configProvider.Config.Thresholds.Full = full;

        // Initialize with a denomination
        configProvider.Config.Inventory["JPY"] = new InventorySettings
        {
            Denominations = new()
            {
                ["C100"] = new() { InitialCount = 50, NearEmpty = nearEmpty, NearFull = nearFull, Full = full }
            }
        };

        var inv = new Inventory();
        var hw = new HardwareStatusManager();
        var history = new TransactionHistory();
        var manager = new CashChangerManager(inv, history, new ChangeCalculator());
        var metadataProvider = new CurrencyMetadataProvider(configProvider);

        // Initialize all denominations to 50 to ensure they are NOT Empty/NearEmpty initially
        foreach (var key in metadataProvider.SupportedDenominations)
        {
            inv.SetCount(key, 50);
        }

        var monitorsProvider = new MonitorsProvider(inv, configProvider, metadataProvider);
        var aggregatorProvider = new OverallStatusAggregatorProvider(monitorsProvider);
        var depositController = new DepositController(inv, hw);
        var dispenseController = new DispenseController(manager, hw, new Mock<IDeviceSimulator>().Object);

        var cc = new SimulatorCashChanger(configProvider, inv, history, manager, depositController, dispenseController, aggregatorProvider, hw)
        {
            SkipStateVerification = true
        };

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

    /// <summary>在庫が空になった際に StatusUpdateEvent が発火することを検証する。</summary>
    [Fact]
    public void ShouldFireStatusUpdateEventWhenDeviceStatusBecomesEmpty()
    {
        var (cc, inv, hw, events) = CreateTestCashChanger();

        // Reduce count to 0 → should trigger Empty status update
        inv.SetCount(Coin100, 0);

        // Allow reactive pipeline to propagate
        Thread.Sleep(TestTimingConstants.EventPropagationDelayMs);

        events.ShouldContain((int)UposCashChangerStatusUpdateCode.Empty);
    }

    /// <summary>在庫が NearEmpty しきい値に達した際に StatusUpdateEvent が発火することを検証する。</summary>
    [Fact]
    public void ShouldFireStatusUpdateEventWhenDeviceStatusBecomesNearEmpty()
    {
        var (cc, inv, hw, events) = CreateTestCashChanger();

        // Reduce count to NearEmpty threshold
        inv.SetCount(Coin100, 3);

        Thread.Sleep(TestTimingConstants.EventPropagationDelayMs);

        events.ShouldContain((int)UposCashChangerStatusUpdateCode.NearEmpty);
    }

    /// <summary>在庫が空の状態から復帰した際に StatusUpdateEvent が発火することを検証する。</summary>
    [Fact]
    public void ShouldFireStatusUpdateEventWhenDeviceStatusRecoversFromEmpty()
    {
        var (cc, inv, hw, events) = CreateTestCashChanger();

        // Go to Empty
        inv.SetCount(Coin100, 0);
        Thread.Sleep(TestTimingConstants.EventPropagationDelayMs);
        events.Clear();

        // Recover
        inv.SetCount(Coin100, 50);
        Thread.Sleep(TestTimingConstants.EventPropagationDelayMs);

        events.ShouldContain((int)UposCashChangerStatusUpdateCode.EmptyOk);
    }

    // ========== FullStatus OK transitions ==========

    /// <summary>在庫がフルの状態から復帰した際に StatusUpdateEvent が発火することを検証する。</summary>
    [Fact]
    public void ShouldFireStatusUpdateEventWhenFullStatusRecoversFromFull()
    {
        var (cc, inv, hw, events) = CreateTestCashChanger();

        // Go to Full
        inv.SetCount(Coin100, 100);
        Thread.Sleep(TestTimingConstants.EventPropagationDelayMs);
        events.Clear();

        // Recover
        inv.SetCount(Coin100, 50);
        Thread.Sleep(TestTimingConstants.EventPropagationDelayMs);

        events.ShouldContain((int)UposCashChangerStatusUpdateCode.FullOk);
    }

    // ========== Jam StatusUpdateEvent ==========

    /// <summary>ジャムが発生した際に標準のジャムコードで StatusUpdateEvent が発火することを検証する。</summary>
    [Fact]
    public void ShouldFireStatusUpdateEventWithStandardJamCodeWhenJammed()
    {
        var (cc, inv, hw, events) = CreateTestCashChanger();

        hw.SetJammed(true);
        Thread.Sleep(TestTimingConstants.EventPropagationDelayMs);

        // UPOS standard: CHAN_STATUS_JAM = 31
        events.ShouldContain((int)UposCashChangerStatusUpdateCode.Jam);
    }

    /// <summary>ジャムが解消された際に OK コードで StatusUpdateEvent が発火することを検証する。</summary>
    [Fact]
    public void ShouldFireStatusUpdateEventWithOkCodeWhenJamCleared()
    {
        var (cc, inv, hw, events) = CreateTestCashChanger();

        hw.SetJammed(true);
        Thread.Sleep(TestTimingConstants.EventPropagationDelayMs);
        events.Clear();

        hw.SetJammed(false);
        Thread.Sleep(TestTimingConstants.EventPropagationDelayMs);

        // UPOS standard: CHAN_STATUS_OK = 0
        events.ShouldContain((int)UposCashChangerStatusUpdateCode.Ok);
    }
}
