using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Tests.Device;

/// <summary>
/// 各種エラーシナリオ（ビジー、不正なパラメータ/シーケンス、在庫不足、ジャム）の検証テスト。
/// </summary>
public class ErrorScenarioTests
{
    private (SimulatorCashChanger Device, HardwareStatusManager Hardware) CreateDevice()
    {
        var config = new SimulatorConfiguration();
        var inventory = new Inventory();
        var history = new TransactionHistory();
        var manager = new CashChangerManager(inventory, history);
        var hardware = new HardwareStatusManager();
        var device = new SimulatorCashChanger(config, inventory, history, manager, null, null, hardware);
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
        lastStatus.ShouldBe(205); // CHAN_STATUS_JAM

        // Jam OFF
        hardware.SetJammed(false);
        lastStatus.ShouldBe(206); // CHAN_STATUS_OK
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
}
