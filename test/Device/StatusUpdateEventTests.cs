using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device.PosForDotNet;
using CashChangerSimulator.Device.PosForDotNet.Models;
using CashChangerSimulator.Device.Virtual;
using Microsoft.PointOfService;
using Microsoft.Extensions.Logging.Abstractions;
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
        var jpySettings = new InventorySettings();
        jpySettings.Denominations.Add("C100", new() { InitialCount = 50, NearEmpty = nearEmpty, NearFull = nearFull, Full = full });
        configProvider.Config.Inventory["JPY"] = jpySettings;

        var inv = Inventory.Create();
        var hw = HardwareStatusManager.Create();
        var history = new TransactionHistory();
        var manager = new CashChangerManager(inv, history, (object?)null, null);
        var metadataProvider = CurrencyMetadataProvider.Create(configProvider);

        // Initialize all denominations to 50 to ensure they are NOT Empty/NearEmpty initially
        foreach (var key in metadataProvider.SupportedDenominations)
        {
            inv.SetCount(key, 50);
        }

        var monitorsProvider = MonitorsProvider.Create(inv, configProvider, metadataProvider);
        var aggregatorProvider = new OverallStatusAggregatorProvider(monitorsProvider);
        var depositController = new DepositController(inv, hw);
        var dispenseController = new DispenseController(manager, inv, configProvider, NullLoggerFactory.Instance, hw, new Mock<IDeviceSimulator>().Object, (TimeProvider?)null);

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
            SkipStateVerification = true
        };

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
    public async Task ShouldFireStatusUpdateEventWhenDeviceStatusBecomesEmpty()
    {
        var (_, inv, _, events) = CreateTestCashChanger();

        // Reduce count to 0 → should trigger Empty status update
        inv.SetCount(Coin100, 0);

        // Wait for the asynchronous event to propagate (max 2s)
        await WaitUntil(() => events.Contains((int)UposCashChangerStatusUpdateCode.Empty));

        events.ShouldContain((int)UposCashChangerStatusUpdateCode.Empty);
    }

    /// <summary>在庫が NearEmpty しきい値に達した際に StatusUpdateEvent が発火することを検証する。</summary>
    [Fact]
    public async Task ShouldFireStatusUpdateEventWhenDeviceStatusBecomesNearEmpty()
    {
        var (_, inv, _, events) = CreateTestCashChanger();

        // Reduce count to NearEmpty threshold
        inv.SetCount(Coin100, 3);

        await WaitUntil(() => events.Contains((int)UposCashChangerStatusUpdateCode.NearEmpty));

        events.ShouldContain((int)UposCashChangerStatusUpdateCode.NearEmpty);
    }

    /// <summary>在庫が空の状態から復帰した際に StatusUpdateEvent が発火することを検証する。</summary>
    [Fact]
    public async Task ShouldFireStatusUpdateEventWhenDeviceStatusRecoversFromEmpty()
    {
        var (_, inv, _, events) = CreateTestCashChanger();

        // Go to Empty
        inv.SetCount(Coin100, 0);
        await WaitUntil(() => events.Contains((int)UposCashChangerStatusUpdateCode.Empty));
        events.Clear();

        // Recover
        inv.SetCount(Coin100, 50);
        await WaitUntil(() => events.Contains((int)UposCashChangerStatusUpdateCode.EmptyOk));

        events.ShouldContain((int)UposCashChangerStatusUpdateCode.EmptyOk);
    }

    // ========== FullStatus OK transitions ==========

    /// <summary>在庫がフルの状態から復帰した際に StatusUpdateEvent が発火することを検証する。</summary>
    [Fact]
    public async Task ShouldFireStatusUpdateEventWhenFullStatusRecoversFromFull()
    {
        var (_, inv, _, events) = CreateTestCashChanger();

        // Go to Full
        inv.SetCount(Coin100, 100);
        await WaitUntil(() => events.Contains((int)UposCashChangerStatusUpdateCode.Full));
        events.Clear();

        // Recover
        inv.SetCount(Coin100, 50);
        await WaitUntil(() => events.Contains((int)UposCashChangerStatusUpdateCode.FullOk));

        events.ShouldContain((int)UposCashChangerStatusUpdateCode.FullOk);
    }

    // ========== Jam StatusUpdateEvent ==========

    /// <summary>ジャムが発生した際に標準のジャムコードで StatusUpdateEvent が発火することを検証する。</summary>
    [Fact]
    public async Task ShouldFireStatusUpdateEventWithStandardJamCodeWhenJammed()
    {
        var (_, _, hw, events) = CreateTestCashChanger();

        hw.SetJammed(true);
        await WaitUntil(() => events.Contains((int)UposCashChangerStatusUpdateCode.Jam));

        // UPOS standard: CHAN_STATUS_JAM = 31
        events.ShouldContain((int)UposCashChangerStatusUpdateCode.Jam);
    }

    /// <summary>ジャムが解消された際に OK コードで StatusUpdateEvent が発火することを検証する。</summary>
    [Fact]
    public async Task ShouldFireStatusUpdateEventWithOkCodeWhenJamCleared()
    {
        var (_, _, hw, events) = CreateTestCashChanger();

        hw.SetJammed(true);
        await WaitUntil(() => events.Contains((int)UposCashChangerStatusUpdateCode.Jam));
        events.Clear();

        hw.SetJammed(false);
        
        await WaitUntil(() => events.Contains((int)UposCashChangerStatusUpdateCode.Ok));

        // UPOS standard: CHAN_STATUS_OK = 0
        events.ShouldContain((int)UposCashChangerStatusUpdateCode.Ok);
    }

    // ========== Physical Unit (Collection Box) StatusUpdateEvent ==========

    /// <summary>回収庫が取り外された際に StatusUpdateEvent (REMOVED) が発火することを検証する。</summary>
    [Fact]
    public async Task ShouldFireStatusUpdateEventWhenCollectionBoxIsRemoved()
    {
        var (_, _, hw, events) = CreateTestCashChanger();

        // Simulate collection box removal
        hw.SetCollectionBoxRemoved(true);
        
        await WaitUntil(() => events.Contains((int)UposCashChangerStatusUpdateCode.Removed));

        events.ShouldContain((int)UposCashChangerStatusUpdateCode.Removed);
    }

    /// <summary>回収庫が装着された際に StatusUpdateEvent (INSERTED) が発火することを検証する。</summary>
    [Fact]
    public async Task ShouldFireStatusUpdateEventWhenCollectionBoxIsInserted()
    {
        var (_, _, hw, events) = CreateTestCashChanger();

        hw.SetCollectionBoxRemoved(true);
        await WaitUntil(() => events.Contains((int)UposCashChangerStatusUpdateCode.Removed));
        events.Clear();

        hw.SetCollectionBoxRemoved(false);
        
        await WaitUntil(() => events.Contains((int)UposCashChangerStatusUpdateCode.Inserted));

        events.ShouldContain((int)UposCashChangerStatusUpdateCode.Inserted);
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
