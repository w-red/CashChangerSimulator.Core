using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using R3;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>Deposit シーケンスの検証テスト。</summary>
/// <remarks>
/// UPOS 8.3.4 シーケンス図 / 8.3.5 状態遷移図に基づく検証を行います。
/// </remarks>
/// <summary>Test class for providing DepositSequenceTests functionality.</summary>
public class DepositSequenceTests
{
    private static (DepositController Controller, Inventory Inventory) CreateController()
    {
        var inventory = new Inventory();
        var history = new TransactionHistory();
        _ = new CashChangerManager(inventory, history, new ChangeCalculator());
        var hw = new HardwareStatusManager();
        hw.SetConnected(true);
        var controller = new DepositController(inventory, hw);
        return (controller, inventory);
    }

    // =====================================================
    // 正常シーケンス
    // =====================================================

    /// <summary>beginDepositから釣銭ありendDepositまでの正常な一連のシーケンスを検証する。</summary>
    [Fact]
    public void FullDepositSequenceWithChange()
    {
        var (controller, _) = CreateController();
        var b1000 = new DenominationKey(1000, CurrencyCashType.Bill);

        // beginDeposit: 入金受付開始
        controller.BeginDeposit();
        controller.DepositStatus.ShouldBe(CashDepositStatus.Count);

        // 入金シミュレーション
        controller.TrackDeposit(b1000);

        controller.DepositAmount.ShouldBe(1000);
        controller.DepositCounts[b1000].ShouldBe(1);

        // fixDeposit: 入金確定
        controller.FixDeposit();
        controller.DepositStatus.ShouldBe(CashDepositStatus.Count);

        // endDeposit(Change): 釣銭分を払い出す
        // ここでは入金分1000円すべてを釣銭対象として想定（manager.Dispenseが呼ばれる）
        controller.EndDeposit(CashDepositAction.Change);
        controller.DepositStatus.ShouldBe(CashDepositStatus.End);
    }

    /// <summary>beginDepositから釣銭なしendDepositまでの正常な一連のシーケンスを検証する。</summary>
    [Fact]
    public void FullDepositSequenceWithNoChange()
    {
        var (controller, inventory) = CreateController();
        var b1000 = new DenominationKey(1000, CurrencyCashType.Bill);

        controller.BeginDeposit();

        controller.TrackDeposit(b1000);

        controller.FixDeposit();

        controller.EndDeposit(CashDepositAction.NoChange);
        controller.DepositStatus.ShouldBe(CashDepositStatus.End);
        inventory.GetCount(b1000).ShouldBe(1); // 在庫に残る
    }

    /// <summary>beginDepositから返却(Repay)を伴うendDepositまでのシーケンスを検証する。</summary>
    [Fact]
    public void FullDepositSequenceWithRepay()
    {
        var (controller, inventory) = CreateController();
        var b1000 = new DenominationKey(1000, CurrencyCashType.Bill);

        controller.BeginDeposit();

        controller.TrackDeposit(b1000);

        controller.FixDeposit();

        // Repay: 入金された現金を全額返却
        controller.EndDeposit(CashDepositAction.Repay);

        controller.DepositStatus.ShouldBe(CashDepositStatus.End);
        inventory.GetCount(b1000).ShouldBe(0); // 在庫から消える（返却）
    }

    /// <summary>入金中の中断(Pause)と再開(Restart)を含むシーケンスを検証する。</summary>
    [Fact]
    public void PauseAndRestartDuringDeposit()
    {
        var (controller, _) = CreateController();
        var b1000 = new DenominationKey(1000, CurrencyCashType.Bill);

        controller.BeginDeposit();

        // Pause
        controller.PauseDeposit(CashDepositPause.Pause);
        controller.IsPaused.ShouldBeTrue();

        // Pause中に在庫が増えてもTrackDepositは無視するはず
        controller.TrackDeposit(b1000);
        controller.DepositAmount.ShouldBe(0);

        // Restart
        controller.PauseDeposit(CashDepositPause.Restart);
        controller.IsPaused.ShouldBeFalse();

        // 再開後に入金
        controller.TrackDeposit(b1000);
        controller.DepositAmount.ShouldBe(1000);

        controller.FixDeposit();
        controller.EndDeposit(CashDepositAction.NoChange);
        controller.DepositStatus.ShouldBe(CashDepositStatus.End);
    }

    // =====================================================
    // 異常シーケンス（E_ILLEGAL ガード）
    // =====================================================

    /// <summary>fixDepositを呼ばずにendDepositを実行した際にErrorCode.Illegalがスローされることを検証する。</summary>
    [Fact]
    public void EndDepositWithoutFixDepositThrowsIllegal()
    {
        var (controller, _) = CreateController();

        controller.BeginDeposit();

        var ex = Should.Throw<PosControlException>(() =>
            controller.EndDeposit(CashDepositAction.NoChange));
        ex.ErrorCode.ShouldBe(ErrorCode.Illegal);
    }

    /// <summary>beginDepositを呼ばずにfixDepositを実行した際にErrorCode.Illegalがスローされることを検証する。</summary>
    [Fact]
    public void FixDepositWithoutBeginDepositThrowsIllegal()
    {
        var (controller, _) = CreateController();

        var ex = Should.Throw<PosControlException>(() =>
            controller.FixDeposit());
        ex.ErrorCode.ShouldBe(ErrorCode.Illegal);
    }

    /// <summary>入金シーケンスの各段階でIsDepositInProgressフラグが正しく更新されることを検証する。</summary>
    [Fact]
    public void DepositInProgressGuardTest()
    {
        var (controller, _) = CreateController();

        controller.IsDepositInProgress.ShouldBeFalse();

        controller.BeginDeposit();
        controller.IsDepositInProgress.ShouldBeTrue();

        controller.FixDeposit();
        controller.IsDepositInProgress.ShouldBeTrue();

        controller.EndDeposit(CashDepositAction.NoChange);
        controller.IsDepositInProgress.ShouldBeFalse();
    }

    // =====================================================
    // オーバーフロー・バリデーション検証
    // =====================================================

    /// <summary>在庫が満杯(Full)の状態で入金した際、オーバーフローとしてカウントされることを検証する。</summary>
    [Fact]
    public void TrackDepositShouldOverflow_WhenInventoryIsFull()
    {
        // Arrange
        var inventory = new Inventory();
        var hw = new HardwareStatusManager();
        hw.SetConnected(true);
        var b1000 = new DenominationKey(1000, CurrencyCashType.Bill);
        
        var config = new SimulatorConfiguration();
        config.Inventory[b1000.CurrencyCode] = new InventorySettings
        {
            Denominations = new Dictionary<string, DenominationSettings>
            {
                { b1000.ToDenominationString(), new DenominationSettings { Full = 5, IsRecyclable = true } }
            }
        };
        var configProvider = new ConfigurationProvider();
        configProvider.Update(config);

        var controller = new DepositController(inventory, hw, null, configProvider);
        inventory.SetCount(b1000, 5); // Already Full

        controller.BeginDeposit();

        // Act
        controller.TrackDeposit(b1000);

        // Assert
        controller.DepositAmount.ShouldBe(1000);
        controller.OverflowAmount.ShouldBe(1000); // 満杯なのでオーバーフローに回る
        controller.DepositCounts[b1000].ShouldBe(1);
    }

    /// <summary>デバイスがジャム(Jam)状態の時に入金を試みた際、例外がスローされることを検証する。</summary>
    [Fact]
    public void TrackDepositShouldThrow_WhenDeviceIsJammed()
    {
        // Arrange
        var (controller, _) = CreateController();
        var hwField = typeof(DepositController).GetField("_hardwareStatusManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var hw = (HardwareStatusManager)hwField.GetValue(controller)!;
        
        controller.BeginDeposit();
        hw.SetJammed(true);

        // Act & Assert
        var ex = Should.Throw<PosControlException>(() => controller.TrackDeposit(new DenominationKey(1000, CurrencyCashType.Bill)));
        ex.ErrorCode.ShouldBe(ErrorCode.Extended);
    }

    /// <summary>入金中にリジェクトイベント（SimulateReject）が発生した際、リジェクト金額が加算され、Changedイベントが発火することを検証する。</summary>
    [Fact]
    public void SimulateRejectShouldIncreaseRejectAmountWhenInProgress()
    {
        var (controller, _) = CreateController();
        bool changedEventFired = false;
        controller.Changed.Subscribe(_ => changedEventFired = true);

        // Assert Before Start
        controller.SimulateReject(1000); // Should be ignored since not in Count status
        controller.RejectAmount.ShouldBe(0m);
        changedEventFired.ShouldBeFalse();

        // Arrange & Act (Start Deposit)
        controller.BeginDeposit();
        changedEventFired = false; // Reset changed event flag after BeginDeposit

        // Act (Simulate Reject)
        controller.SimulateReject(1000);

        // Assert
        controller.RejectAmount.ShouldBe(1000m);
        changedEventFired.ShouldBeTrue();
    }
}
