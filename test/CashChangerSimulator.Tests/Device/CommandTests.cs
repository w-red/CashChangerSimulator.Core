using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Commands;
using CashChangerSimulator.Device.Coordination;
using Microsoft.PointOfService;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

public class CommandTests
{
    private readonly Inventory _inventory;
    private readonly HardwareStatusManager _hardware;
    private readonly Mock<IUposMediator> _mediator;

    public CommandTests()
    {
        _inventory = new Inventory();
        _hardware = new HardwareStatusManager();
        _mediator = new Mock<IUposMediator>();
    }

    [Fact]
    public void AdjustCashCountsCommand_ShouldThrowWhenJammed()
    {
        var counts = new List<CashCount> { new CashCount(CashCountType.Bill, 1000, 10) };
        var cmd = new AdjustCashCountsCommand(_inventory, counts, "JPY", 1, _hardware);
        
        _hardware.SetJammed(true);
        var ex = Should.Throw<PosControlException>(() => cmd.Execute());
        ex.ErrorCode.ShouldBe(ErrorCode.Extended);
    }

    [Fact]
    public void DispenseCashCommand_Verify_ShouldThrowWhenJammed()
    {
        var deposit = new DepositController(_inventory, _hardware);
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        _inventory.SetCount(key, 10);
        var counts = new Dictionary<DenominationKey, int> { { key, 1 } };
        var cmd = new DispenseCashCommand(null!, _inventory, _hardware, deposit, counts, false, null!);

        _hardware.SetJammed(true);
        var ex = Should.Throw<PosControlException>(() => cmd.Verify(_mediator.Object));
        ex.ErrorCode.ShouldBe(ErrorCode.Extended);
    }

    [Fact]
    public void DispenseCashCommand_Verify_ShouldThrowWhenDepositInProgress()
    {
        var deposit = new DepositController(_inventory, _hardware);
        deposit.BeginDeposit(); // Sets IsDepositInProgress to true
        
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        _inventory.SetCount(key, 10);
        var counts = new Dictionary<DenominationKey, int> { { key, 1 } };
        var cmd = new DispenseCashCommand(null!, _inventory, _hardware, deposit, counts, false, null!);

        var ex = Should.Throw<PosControlException>(() => cmd.Verify(_mediator.Object));
        ex.ErrorCode.ShouldBe(ErrorCode.Illegal);
    }

    [Fact]
    public void DispenseCashCommand_Verify_ShouldThrowWhenDenominationNotRegistered()
    {
        var deposit = new DepositController(_inventory, _hardware);
        var counts = new Dictionary<DenominationKey, int> { { new DenominationKey(999, CurrencyCashType.Bill), 1 } };
        var cmd = new DispenseCashCommand(null!, _inventory, _hardware, deposit, counts, false, null!);

        var ex = Should.Throw<PosControlException>(() => cmd.Verify(_mediator.Object));
        ex.ErrorCode.ShouldBe(ErrorCode.Illegal);
    }

    [Fact]
    public void DispenseCashCommand_Verify_ShouldThrowWhenInsufficientInventory()
    {
        var deposit = new DepositController(_inventory, _hardware);
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        _inventory.SetCount(key, 0);
        var counts = new Dictionary<DenominationKey, int> { { key, 1 } };
        var cmd = new DispenseCashCommand(null!, _inventory, _hardware, deposit, counts, false, null!);

        var ex = Should.Throw<PosControlException>(() => cmd.Verify(_mediator.Object));
        ex.ErrorCode.ShouldBe(ErrorCode.Extended);
    }
}
