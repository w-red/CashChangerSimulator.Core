using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device.Virtual;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>入金シーケンスの各状態遷移(UPOS準拠)を検証するテストクラス。</summary>
public class DepositSequenceTests
{
    private static (DepositController Controller, Inventory Inventory, HardwareStatusManager Hardware) CreateController()
    {
        var inventory = Inventory.Create();
        var history = new TransactionHistory();
        var configProvider = new ConfigurationProvider();
        configProvider.Config.Simulation.DepositDelayMs = 0;
        var manager = new CashChangerManager(inventory, history, configProvider);
        var hw = HardwareStatusManager.Create();
        hw.Input.IsConnected.Value = true;
        var loggerFactory = new LoggerFactory();
        var simulator = new Mock<IDeviceSimulator>().Object;
        var controller = new DepositController(manager, inventory, hw, configProvider, loggerFactory);
        return (controller, inventory, hw);
    }

    // =====================================================
    // 正常シーケンス
    // =====================================================

    /// <summary>beginDepositから釣銭ありendDepositまでの正常な一連のシーケンスを検証する。</summary>
    [Fact]
    public void FullDepositSequenceWithChange()
    {
        var (controller, _, _) = CreateController();
        var b1000 = new DenominationKey(1000, CurrencyCashType.Bill);

        // beginDeposit: 入金受付開始
        controller.BeginDeposit();
        controller.DepositStatus.ShouldBe(DeviceDepositStatus.Counting);

        // 入金シミュレーション
        controller.TrackDeposit(b1000);

        controller.DepositAmount.ShouldBe(1000);
        controller.DepositCounts[b1000].ShouldBe(1);

        // fixDeposit: 入金確定
        controller.FixDeposit();
        controller.DepositStatus.ShouldBe(DeviceDepositStatus.Counting);

        // endDeposit(Change): 釣銭分を払い出す
        // 1000円投入、必要金額0円の場合、全額1000円が釣銭対象となる
        controller.EndDeposit(DepositAction.Change);
        controller.DepositStatus.ShouldBe(DeviceDepositStatus.End);
    }

    /// <summary>beginDepositから釣銭なしendDepositまでの正常な一連のシーケンスを検証する。</summary>
    [Fact]
    public void FullDepositSequenceWithNoChange()
    {
        var (controller, inventory, _) = CreateController();
        var b1000 = new DenominationKey(1000, CurrencyCashType.Bill);

        controller.BeginDeposit();

        controller.TrackDeposit(b1000);

        controller.FixDeposit();

        controller.EndDeposit(DepositAction.NoChange);
        controller.DepositStatus.ShouldBe(DeviceDepositStatus.End);
        inventory.GetCount(b1000).ShouldBe(1); // 在庫に残る
    }

    /// <summary>beginDepositから返却(Repay)を伴うendDepositまでのシーケンスを検証する。</summary>
    [Fact]
    public void FullDepositSequenceWithRepay()
    {
        var (controller, inventory, _) = CreateController();
        var b1000 = new DenominationKey(1000, CurrencyCashType.Bill);

        controller.BeginDeposit();

        controller.TrackDeposit(b1000);

        controller.FixDeposit();

        // Repay: 入金された現金を全額返却
        controller.EndDeposit(DepositAction.Repay);

        controller.DepositStatus.ShouldBe(DeviceDepositStatus.End);
        inventory.GetCount(b1000).ShouldBe(0); // 在庫から消える(返却)
    }

    /// <summary>入金中の中断(Pause)と再開(Restart)を含むシーケンスを検証する。</summary>
    [Fact]
    public void PauseAndRestartDuringDeposit()
    {
        var (controller, _, _) = CreateController();
        var b1000 = new DenominationKey(1000, CurrencyCashType.Bill);

        controller.BeginDeposit();

        // Pause
        controller.PauseDeposit(DeviceDepositPause.Pause);
        controller.IsPaused.ShouldBeTrue();

        // Pause中に在庫が増えてもTrackDepositは無視するはず
        controller.TrackDeposit(b1000);
        controller.DepositAmount.ShouldBe(0);

        // Restart
        controller.PauseDeposit(DeviceDepositPause.Resume);
        controller.IsPaused.ShouldBeFalse();

        // 再開後に入金
        controller.TrackDeposit(b1000);
        controller.DepositAmount.ShouldBe(1000);

        controller.FixDeposit();
        controller.EndDeposit(DepositAction.NoChange);
        controller.DepositStatus.ShouldBe(DeviceDepositStatus.End);
    }

    // =====================================================
    // 異常シーケンス(E_ILLEGAL ガード)
    // =====================================================

    /// <summary>fixDepositを呼ばずにendDepositを実行した際にErrorCode.Illegalがスローされることを検証する。</summary>
    [Fact]
    public void EndDepositWithoutFixDepositThrowsIllegal()
    {
        var (controller, _, _) = CreateController();

        controller.BeginDeposit();

        var ex = Should.Throw<DeviceException>(() =>
            controller.EndDeposit(DepositAction.NoChange));
        ex.ErrorCode.ShouldBe(DeviceErrorCode.Illegal);
    }

    /// <summary>beginDepositを呼ばずにfixDepositを実行した際にErrorCode.Illegalがスローされることを検証する。</summary>
    [Fact]
    public void FixDepositWithoutBeginDepositThrowsIllegal()
    {
        var (controller, _, _) = CreateController();

        var ex = Should.Throw<DeviceException>(controller.FixDeposit);
        ex.ErrorCode.ShouldBe(DeviceErrorCode.Illegal);
    }

    /// <summary>入金シーケンスの各段階でIsDepositInProgressフラグが正しく更新されることを検証する。</summary>
    [Fact]
    public void DepositInProgressGuardTest()
    {
        var (controller, _, _) = CreateController();

        controller.IsDepositInProgress.ShouldBeFalse();

        controller.BeginDeposit();
        controller.IsDepositInProgress.ShouldBeTrue();

        controller.FixDeposit();
        controller.IsDepositInProgress.ShouldBeTrue();

        controller.EndDeposit(DepositAction.NoChange);
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
        var inventory = Inventory.Create();
        var hw = HardwareStatusManager.Create();
        hw.Input.IsConnected.Value = true;
        var b1000 = new DenominationKey(1000, CurrencyCashType.Bill);

        var config = new SimulatorConfiguration();
        var inventorySettings = new InventorySettings();
        inventorySettings.Denominations.Add(b1000.ToDenominationString(), new DenominationSettings { Full = 5, IsRecyclable = true });
        config.Inventory[b1000.CurrencyCode] = inventorySettings;
        var configProvider = new ConfigurationProvider();
        configProvider.Update(config);

        var loggerFactory = new LoggerFactory();
        var simulator = new Mock<IDeviceSimulator>().Object;
        var manager = new CashChangerManager(inventory, new TransactionHistory(), configProvider);
        var controller = new DepositController(manager, inventory, hw, configProvider, loggerFactory);
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
        var (controller, _, hw) = CreateController();

        controller.BeginDeposit();
        hw.Input.IsJammed.Value = true;

        // Act & Assert
        var ex = Should.Throw<DeviceException>(() => controller.TrackDeposit(new DenominationKey(1000, CurrencyCashType.Bill)));
        ex.ErrorCode.ShouldBe(DeviceErrorCode.Jammed);
    }

    /// <summary>投入された現金がリアルタイムでエスクロー在庫を更新することを検証します。</summary>
    [Fact]
    public void TrackDepositShouldUpdateInventoryEscrow()
    {
        var (controller, inventory, _) = CreateController();
        var b1000 = new DenominationKey(1000, CurrencyCashType.Bill);

        controller.BeginDeposit();
        controller.TrackDeposit(b1000);

        // Escrow should be updated in real-time
        inventory.EscrowCounts.ShouldContain(kv => kv.Key == b1000 && kv.Value == 1);
        inventory.GetCount(b1000).ShouldBe(0); // Not in main inventory yet
    }

    /// <summary>釣銭返却アクション(EndDeposit Change)時にエスクロー内の特定金種が優先的に扱われることを検証します。</summary>
    [Fact]
    public void EndDepositChangeShouldDispenseFromEscrowFirst()
    {
        var (controller, inventory, _) = CreateController();
        var b1000 = new DenominationKey(1000, CurrencyCashType.Bill);
        var c10 = new DenominationKey(10, CurrencyCashType.Coin);

        // Required Amount: 1050
        controller.RequiredAmount = 1050;

        controller.BeginDeposit();

        // Input: 5x1000 bills, 7x10 coins = 5070
        for (int i = 0; i < 5; i++)
        {
            controller.TrackDeposit(b1000);
        }

        for (int i = 0; i < 7; i++)
        {
            controller.TrackDeposit(c10);
        }

        controller.FixDeposit();

        // Total: 5070, Required: 1050 -> Change: 4020
        // Escrow-First Return: 4x1000 bills, 2x10 coins should be returned (stay out of inventory)
        // 1x1000 bill, 5x10 coins should go to inventory
        controller.EndDeposit(DepositAction.Change);

        inventory.GetCount(b1000).ShouldBe(1);
        inventory.GetCount(c10).ShouldBe(5);
        inventory.EscrowCounts.ShouldBeEmpty();
    }

    /// <summary>入金キャンセル(Repay)時にエスクロー在庫がクリアされることを検証します。</summary>
    [Fact]
    public void EndDepositRepayShouldClearInventoryEscrow()
    {
        var (controller, inventory, _) = CreateController();
        var b1000 = new DenominationKey(1000, CurrencyCashType.Bill);

        controller.BeginDeposit();
        controller.TrackDeposit(b1000);
        controller.FixDeposit();
        controller.EndDeposit(DepositAction.Repay);

        inventory.GetCount(b1000).ShouldBe(0);
        inventory.EscrowCounts.ShouldBeEmpty();
    }
}
