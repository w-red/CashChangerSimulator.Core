using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Coordination;
using Microsoft.PointOfService;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>InternalSimulatorCashChanger の StatusUpdateEvent 発火を検証するテストクラス (TDD Red)。</summary>
public class StatusUpdateEventTests
{
    private static readonly DenominationKey Coin100 = new(100, CurrencyCashType.Coin, "JPY");

    /// <summary>テスト用の InternalSimulatorCashChanger を生成する。</summary>
    private static (InternalSimulatorCashChanger cc, Inventory inv, HardwareStatusManager hw, List<int> events) CreateTestCashChanger(
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

        var deps = new SimulatorDependencies(
            configProvider,
            inv,
            history,
            manager,
            depositController,
            dispenseController,
            aggregatorProvider,
            hw);

        var cc = new InternalSimulatorCashChanger(deps)
        {
            // SkipStateVerification = true
        };
        cc.SkipStateVerification = true;
        // cc.Open(); -- StatusUpdateTests handles Open manually if needed or via CreateTestCashChanger

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
        var (_, inv, _, events) = CreateTestCashChanger();

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
        var (_, inv, _, events) = CreateTestCashChanger();

        // Reduce count to NearEmpty threshold
        inv.SetCount(Coin100, 3);

        Thread.Sleep(TestTimingConstants.EventPropagationDelayMs);

        events.ShouldContain((int)UposCashChangerStatusUpdateCode.NearEmpty);
    }

    /// <summary>在庫が空の状態から復帰した際に StatusUpdateEvent が発火することを検証する。</summary>
    [Fact]
    public void ShouldFireStatusUpdateEventWhenDeviceStatusRecoversFromEmpty()
    {
        var (_, inv, _, events) = CreateTestCashChanger();

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
        var (_, inv, _, events) = CreateTestCashChanger();

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
        var (_, _, hw, events) = CreateTestCashChanger();

        hw.SetJammed(true);
        Thread.Sleep(TestTimingConstants.EventPropagationDelayMs);

        // UPOS standard: CHAN_STATUS_JAM = 31
        events.ShouldContain((int)UposCashChangerStatusUpdateCode.Jam);
    }

    /// <summary>ジャムが解消された際に OK コードで StatusUpdateEvent が発火することを検証する。</summary>
    [Fact]
    public void ShouldFireStatusUpdateEventWithOkCodeWhenJamCleared()
    {
        var (_, _, hw, events) = CreateTestCashChanger();

        hw.SetJammed(true);
        Thread.Sleep(TestTimingConstants.EventPropagationDelayMs);
        events.Clear();

        hw.SetJammed(false);
        Thread.Sleep(TestTimingConstants.EventPropagationDelayMs);

        // UPOS standard: CHAN_STATUS_OK = 0
        events.ShouldContain((int)UposCashChangerStatusUpdateCode.Ok);
    }

    // ========== Physical Unit (Collection Box) StatusUpdateEvent ==========

    /// <summary>回収庫が取り外された際に StatusUpdateEvent (REMOVED) が発火することを検証する。</summary>
    [Fact]
    public void ShouldFireStatusUpdateEventWhenCollectionBoxIsRemoved()
    {
        var (_, _, hw, events) = CreateTestCashChanger();

        // Simulate collection box removal
        // Note: SetCollectionBoxRemoved is expected to be implemented in HardwareStatusManager
        hw.SetCollectionBoxRemoved(true);
        Thread.Sleep(TestTimingConstants.EventPropagationDelayMs);

        events.ShouldContain((int)UposCashChangerStatusUpdateCode.Removed);
    }

    /// <summary>回収庫が装着された際に StatusUpdateEvent (INSERTED) が発火することを検証する。</summary>
    [Fact]
    public void ShouldFireStatusUpdateEventWhenCollectionBoxIsInserted()
    {
        var (_, _, hw, events) = CreateTestCashChanger();

        hw.SetCollectionBoxRemoved(true);
        Thread.Sleep(TestTimingConstants.EventPropagationDelayMs);
        events.Clear();

        hw.SetCollectionBoxRemoved(false);
        Thread.Sleep(TestTimingConstants.EventPropagationDelayMs);

        events.ShouldContain((int)UposCashChangerStatusUpdateCode.Inserted);
    }
}
