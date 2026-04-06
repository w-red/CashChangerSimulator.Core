using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device.PosForDotNet.Commands;
using CashChangerSimulator.Device.PosForDotNet.Coordination;
using CashChangerSimulator.Device.Virtual;
using Microsoft.PointOfService;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>各コマンド（AdjustCashCounts, DispenseCash 等）の実行前検証と例外処理をテストするクラス。.</summary>
public class CommandTests
{
    private readonly Inventory inventory;
    private readonly HardwareStatusManager hardware;
    private readonly Mock<IUposMediator> mediator;

    public CommandTests()
    {
        inventory = new Inventory();
        hardware = new HardwareStatusManager();
        mediator = new Mock<IUposMediator>();
    }

    /// <summary>デバイスがジャム状態の時に AdjustCashCountsCommand が E_EXT をスローすることを検証します。.</summary>
    [Fact]
    public void AdjustCashCountsCommandShouldThrowWhenJammed()
    {
        var counts = new List<CashCount> { new(CashCountType.Bill, 1000, 10) };
        var cmd = new AdjustCashCountsCommand(inventory, counts, "JPY", 1, hardware);

        hardware.SetJammed(true);
        var ex = Should.Throw<PosControlException>(() => cmd.Execute());
        ex.ErrorCode.ShouldBe(ErrorCode.Extended);
    }

    /// <summary>デバイスがジャム状態の時に DispenseCashCommand の Verify が E_EXT をスローすることを検証します。.</summary>
    [Fact]
    public void DispenseCashCommandVerifyShouldThrowWhenJammed()
    {
        var deposit = new DepositController(inventory, hardware);
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        inventory.SetCount(key, 10);
        var counts = new Dictionary<DenominationKey, int> { { key, 1 } };
        var cmd = new DispenseCashCommand(null!, inventory, hardware, deposit, counts, false);

        hardware.SetJammed(true);
        var ex = Should.Throw<PosControlException>(() => cmd.Verify(mediator.Object));
        ex.ErrorCode.ShouldBe(ErrorCode.Extended);
    }

    /// <summary>入金処理中の際、DispenseCashCommand の Verify が E_ILLEGAL をスローすることを検証します。.</summary>
    [Fact]
    public void DispenseCashCommandVerifyShouldThrowWhenDepositInProgress()
    {
        var deposit = new DepositController(inventory, hardware);
        deposit.BeginDeposit(); // Sets IsDepositInProgress to true

        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        inventory.SetCount(key, 10);
        var counts = new Dictionary<DenominationKey, int> { { key, 1 } };
        var cmd = new DispenseCashCommand(null!, inventory, hardware, deposit, counts, false);

        var ex = Should.Throw<PosControlException>(() => cmd.Verify(mediator.Object));
        ex.ErrorCode.ShouldBe(ErrorCode.Illegal);
    }

    /// <summary>未登録の金種が指定された際、DispenseCashCommand の Verify が E_ILLEGAL をスローすることを検証します。.</summary>
    [Fact]
    public void DispenseCashCommandVerifyShouldThrowWhenDenominationNotRegistered()
    {
        var deposit = new DepositController(inventory, hardware);
        var counts = new Dictionary<DenominationKey, int> { { new DenominationKey(999, CurrencyCashType.Bill), 1 } };
        var cmd = new DispenseCashCommand(null!, inventory, hardware, deposit, counts, false);

        var ex = Should.Throw<PosControlException>(() => cmd.Verify(mediator.Object));
        ex.ErrorCode.ShouldBe(ErrorCode.Illegal);
    }

    /// <summary>在庫不足の際、DispenseCashCommand の Verify が E_EXT をスローすることを検証します。.</summary>
    [Fact]
    public void DispenseCashCommandVerifyShouldThrowWhenInsufficientInventory()
    {
        var deposit = new DepositController(inventory, hardware);
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        inventory.SetCount(key, 0);
        var counts = new Dictionary<DenominationKey, int> { { key, 1 } };
        var cmd = new DispenseCashCommand(null!, inventory, hardware, deposit, counts, false);

        var ex = Should.Throw<PosControlException>(() => cmd.Verify(mediator.Object));
        ex.ErrorCode.ShouldBe(ErrorCode.Extended);
    }
}
