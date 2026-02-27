using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>各種エラーシナリオ（ビジー、不正なパラメータ/シーケンス、在庫不足、ジャム）の検証テスト。</summary>
public class ErrorScenarioTests
{
    private (SimulatorCashChanger Device, HardwareStatusManager Hardware) CreateDevice()
    {
        var configProvider = new ConfigurationProvider();
        var inventory = new Inventory();
        var history = new TransactionHistory();
        var manager = new CashChangerManager(inventory, history, new ChangeCalculator());
        var hardware = new HardwareStatusManager();
        var metadataProvider = new CurrencyMetadataProvider(configProvider);
        var monitorsProvider = new MonitorsProvider(inventory, configProvider, metadataProvider);
        var aggregatorProvider = new OverallStatusAggregatorProvider(monitorsProvider);
        var depositController = new DepositController(inventory, hardware);
        var dispenseController = new DispenseController(manager, hardware, new Mock<IDeviceSimulator>().Object);

        var device = new SimulatorCashChanger(configProvider, inventory, history, manager, depositController, dispenseController, aggregatorProvider, hardware)
        {
            SkipStateVerification = true
        };
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
        ex.ErrorCodeExtended.ShouldBe(201); // ECHAN_OVERDISPENSE
    }

    /// <summary>ジャムが発生している際、払出が ErrorCode.Failure で失敗することを検証する。</summary>
    [Fact]
    public void DispenseDuringJamShouldThrowFailure()
    {
        var (device, hardware) = CreateDevice();
        hardware.SetJammed(true);

        Should.Throw<PosControlException>(() => device.DispenseChange(1000))
            .ErrorCode.ShouldBe(ErrorCode.Failure);
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
        lastStatus.ShouldBe((int)CashChangerSimulator.Core.Opos.UposCashChangerStatusUpdateCode.Jam);

        // Jam OFF
        hardware.SetJammed(false);
        lastStatus.ShouldBe((int)CashChangerSimulator.Core.Opos.UposCashChangerStatusUpdateCode.Ok);
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
        ex.ErrorCodeExtended.ShouldBe(201); // ECHAN_OVERDISPENSE
    }
}
