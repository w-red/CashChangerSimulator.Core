using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>各種エラーシナリオ（ビジー、不正なパラメータ/シーケンス、在庫不足、ジャム）の検証テスト。</summary>
public class ErrorScenarioTests
{
    private (InternalSimulatorCashChanger Device, HardwareStatusManager Hardware) CreateDevice()
    {
        var configProvider = new ConfigurationProvider();
        
        // Ensure USD is in the config for tests that need it
        if (!configProvider.Config.Inventory.ContainsKey("USD"))
        {
            configProvider.Config.Inventory["USD"] = new InventorySettings
            {
                Denominations = new Dictionary<string, DenominationSettings>
                {
                    ["B10"] = new(), ["B5"] = new(), ["B1"] = new(),
                    ["C1"] = new(), ["C0.25"] = new(), ["C0.1"] = new()
                }
            };
        }

        var inventory = new Inventory();
        
        // Initialize inventory with keys from config for JPY and USD
        foreach (var currency in configProvider.Config.Inventory)
        {
            foreach (var denom in currency.Value.Denominations)
            {
                if (DenominationKey.TryParse(denom.Key, currency.Key, out var key) && key != null)
                {
                    inventory.SetCount(key, 0);
                }
            }
        }

        var history = new TransactionHistory();
        var manager = new CashChangerManager(inventory, history, new ChangeCalculator());
        var hardware = new HardwareStatusManager();
        var metadataProvider = new CurrencyMetadataProvider(configProvider);
        var monitorsProvider = new MonitorsProvider(inventory, configProvider, metadataProvider);
        var aggregatorProvider = new OverallStatusAggregatorProvider(monitorsProvider);
        var depositController = new DepositController(inventory, hardware);
        var dispenseController = new DispenseController(manager, hardware, new Mock<IDeviceSimulator>().Object);

        var device = new InternalSimulatorCashChanger(configProvider, inventory, history, manager, depositController, dispenseController, aggregatorProvider, hardware)
        {
            SkipStateVerification = true
        };
        device.Open();
        return (device, hardware);
    }

    /// <summary>DispenseChange に 0 以下の金額を指定した際、ErrorCode.Illegal が発生することを検証する。</summary>
    [Fact]
    public void DispenseChangeWithNegativeAmountShouldThrowIllegal()
    {
        var (device, _) = CreateDevice();
        Should.Throw<PosControlException>(() => device.DispenseChange(0))
            .ErrorCode.ShouldBe(ErrorCode.Illegal);
        Should.Throw<PosControlException>(() => device.DispenseChange(-100))
            .ErrorCode.ShouldBe(ErrorCode.Illegal);
    }

    /// <summary>入金中に払出を試みた際、ErrorCode.Illegal が発生することを検証する。</summary>
    [Fact]
    public void DispenseDuringDepositShouldThrowIllegal()
    {
        var (device, _) = CreateDevice();
        device.BeginDeposit();

        Should.Throw<PosControlException>(() => device.DispenseChange(100))
            .ErrorCode.ShouldBe(ErrorCode.Illegal);
    }

    /// <summary>fixDeposit を呼ばずに endDeposit を実行した際、ErrorCode.Illegal が発生することを検証する。</summary>
    [Fact]
    public void EndDepositWithoutFixDepositShouldThrowIllegal()
    {
        var (device, _) = CreateDevice();
        device.BeginDeposit();

        Should.Throw<PosControlException>(() => device.EndDeposit(CashDepositAction.NoChange))
            .ErrorCode.ShouldBe(ErrorCode.Illegal);
    }

    /// <summary>在庫不足で払出ができない際、ErrorCode.Extended (ECHAN_OVERDISPENSE) が発生することを検証する。</summary>
    [Fact]
    public void DispenseWithShortageShouldThrowOverdispense()
    {
        var (device, _) = CreateDevice();
        // 在庫 0 の状態で払出
        var ex = Should.Throw<PosControlException>(() => device.DispenseChange(1000));
        ex.ErrorCode.ShouldBe(ErrorCode.Extended);
        ex.ErrorCodeExtended.ShouldBe((int)UposCashChangerErrorCodeExtended.OverDispense); // ECHAN_OVERDISPENSE
    }

    /// <summary>ジャムが発生している際、払出が ErrorCode.Failure で失敗することを検証する。</summary>
    [Fact]
    public void DispenseDuringJamShouldThrowFailure()
    {
        var (device, hardware) = CreateDevice();
        hardware.SetJammed(true);

        var ex = Should.Throw<PosControlException>(() => device.DispenseChange(1000));
        ex.ErrorCode.ShouldBe(ErrorCode.Extended);
        ex.ErrorCodeExtended.ShouldBe((int)UposCashChangerErrorCodeExtended.Jam);
    }

    /// <summary>ジャム発生・復旧時に正しい StatusUpdateEvent が発火することを検証する。</summary>
    [Fact]
    public void JamShouldFireStatusUpdateEvent()
    {
        var (device, hardware) = CreateDevice();
        int lastStatus = 0;

        device.OnEventQueued = (e) =>
        {
            if (e is StatusUpdateEventArgs se)
            {
                lastStatus = se.Status;
            }
        };

        // Jam ON
        hardware.SetJammed(true);
        lastStatus.ShouldBe((int)UposCashChangerStatusUpdateCode.Jam);

        // Jam OFF
        hardware.SetJammed(false);
        lastStatus.ShouldBe((int)UposCashChangerStatusUpdateCode.Ok);
    }

    /// <summary>重複した pauseDeposit 呼び出しが ErrorCode.Illegal を発生させることを検証する。</summary>
    [Fact]
    public void DuplicatePauseDepositShouldThrowIllegal()
    {
        var (device, _) = CreateDevice();
        device.BeginDeposit();

        device.PauseDeposit(CashDepositPause.Pause);
        Should.Throw<PosControlException>(() => device.PauseDeposit(CashDepositPause.Pause))
            .ErrorCode.ShouldBe(ErrorCode.Illegal);

        device.PauseDeposit(CashDepositPause.Restart);
        Should.Throw<PosControlException>(() => device.PauseDeposit(CashDepositPause.Restart))
            .ErrorCode.ShouldBe(ErrorCode.Illegal);
    }

    /// <summary>AdjustCashCounts に 0 未満の枚数を指定した際、ErrorCode.Illegal が発生することを検証する。</summary>
    [Fact]
    public void AdjustCashCountsWithNegativeCountShouldThrowIllegal()
    {
        var (device, _) = CreateDevice();
        var counts = new[] { new CashCount(CashCountType.Bill, 1000, -1) };

        Should.Throw<PosControlException>(() => device.AdjustCashCounts(counts))
            .ErrorCode.ShouldBe(ErrorCode.Illegal);
    }

    /// <summary>DispenseCash に 0 未満の枚数を指定した際、ErrorCode.Illegal が発生することを検証する。</summary>
    [Fact]
    public void DispenseCashWithNegativeCountShouldThrowIllegal()
    {
        var (device, _) = CreateDevice();
        var counts = new[] { new CashCount(CashCountType.Bill, 1000, -1) };

        Should.Throw<PosControlException>(() => device.DispenseCash(counts))
            .ErrorCode.ShouldBe(ErrorCode.Illegal);
    }

    /// <summary>DispenseCash で特定の金種が不足している際、ECHAN_OVERDISPENSE が発生することを検証する。</summary>
    [Fact]
    public void DispenseCashWithSpecificShortageShouldThrowOverdispense()
    {
        var (device, _) = CreateDevice();
        // 在庫 0 の金種を指定して払出
        var counts = new[] { new CashCount(CashCountType.Bill, 1000, 1) };

        var ex = Should.Throw<PosControlException>(() => device.DispenseCash(counts));
        ex.ErrorCode.ShouldBe(ErrorCode.Extended);
        ex.ErrorCodeExtended.ShouldBe((int)UposCashChangerErrorCodeExtended.OverDispense); // ECHAN_OVERDISPENSE
    }

    /// <summary>在庫の合計金額は足りているが、金種の組み合わせで端数が支払えない（Impossible Change）場合にエラーになることを検証する。</summary>
    [Fact]
    public void DispenseChangeWithImpossibleCombinationShouldThrowOverdispense()
    {
        var (device, _) = CreateDevice();
        // 1000円札 1枚のみの設定
        device.AdjustCashCounts(new[] { new CashCount(CashCountType.Bill, 1000, 1) });

        // 500円を要求（在庫金額はあるが、組み合わせがない）
        var ex = Should.Throw<PosControlException>(() => device.DispenseChange(500));
        ex.ErrorCode.ShouldBe(ErrorCode.Extended);
        ex.ErrorCodeExtended.ShouldBe((int)UposCashChangerErrorCodeExtended.OverDispense);
    }

    /// <summary>インベントリに登録されていない不正な金種を DispenseCash で要求した際、ErrorCode.Illegal が発生することを検証する。</summary>
    [Fact]
    public void DispenseCashWithUnsupportedDenominationShouldThrowIllegal()
    {
        var (device, _) = CreateDevice();
        // USD設定下かつ標準でない金種（3ドルなど）
        device.CurrencyCode = "USD";
        var counts = new[] { new CashCount(CashCountType.Bill, 3, 1) };

        Should.Throw<PosControlException>(() => device.DispenseCash(counts))
            .ErrorCode.ShouldBe(ErrorCode.Illegal);
    }

    /// <summary>DirectIO (ADJUST_CASH_COUNTS_STR) に不正な引数（nullや非文字列）を渡した際、ErrorCode.Illegal が発生することを検証する。</summary>
    [Fact]
    public void DirectIOAdjustWithInvalidArgumentsShouldThrowIllegal()
    {
        var (device, _) = CreateDevice();
        
        // Non-string object
        Should.Throw<PosControlException>(() => device.DirectIO(DirectIOCommands.AdjustCashCountsStr, 0, 123))
            .ErrorCode.ShouldBe(ErrorCode.Illegal);

        // Null object
        Should.Throw<PosControlException>(() => device.DirectIO(DirectIOCommands.AdjustCashCountsStr, 0, null!))
            .ErrorCode.ShouldBe(ErrorCode.Illegal);
    }

    /// <summary>CashCountParser において不正なセクション数などの境界値を検証する。</summary>
    [Fact]
    public void ParseWithInvalidSectionsShouldThrowIllegal()
    {
        var (device, _) = CreateDevice();
        
        // 3セクション以上
        Should.Throw<PosControlException>(() => device.DirectIO(DirectIOCommands.AdjustCashCountsStr, 0, "1:1;2:2;3:3"))
            .ErrorCode.ShouldBe(ErrorCode.Illegal);

        // セパレータのみ
        Should.Throw<PosControlException>(() => device.DirectIO(DirectIOCommands.AdjustCashCountsStr, 0, ":;:"))
            .ErrorCode.ShouldBe(ErrorCode.Illegal);
    }
}
