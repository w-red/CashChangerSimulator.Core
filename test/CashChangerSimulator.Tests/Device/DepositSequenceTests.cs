using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using MoneyKind4Opos.Currencies.Interfaces;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>
/// UPOS 8.3.4 シーケンス図 / 8.3.5 状態遷移図に基づく
/// Deposit シーケンスの検証テスト。
/// </summary>
public class DepositSequenceTests
{
    private (DepositController Controller, Inventory Inventory) CreateController()
    {
        var inventory = new Inventory();
        var history = new TransactionHistory();
        var manager = new CashChangerManager(inventory, history);
        var controller = new DepositController(inventory);
        return (controller, inventory);
    }

    // =====================================================
    // 正常シーケンス
    // =====================================================

    /// <summary>beginDepositから釣銭ありendDepositまでの正常な一連のシーケンスを検証する。</summary>
    [Fact]
    public void FullDepositSequenceWithChange()
    {
        var (controller, inventory) = CreateController();
        var b1000 = new DenominationKey(1000, CashType.Bill);

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
        var b1000 = new DenominationKey(1000, CashType.Bill);

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
        var b1000 = new DenominationKey(1000, CashType.Bill);

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
        var (controller, inventory) = CreateController();
        var b1000 = new DenominationKey(1000, CashType.Bill);

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
}
