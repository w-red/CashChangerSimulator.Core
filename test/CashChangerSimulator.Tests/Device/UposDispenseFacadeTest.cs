using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using Moq;

namespace CashChangerSimulator.Tests.Device;

/// <summary>UposDispenseFacade の動作を検証するテストクラス。</summary>
public class UposDispenseFacadeTest
{
    private readonly Inventory _inventory;
    private readonly DepositController _depositController;
    private readonly DispenseController _dispenseController;
    private readonly HardwareStatusManager _hardwareStatusManager;
    private readonly UposDispenseFacade _facade;

    public UposDispenseFacadeTest()
    {
        _inventory = new Inventory();
        _inventory.SetCount(new DenominationKey(1000m, CurrencyCashType.Bill, "JPY"), 10);
        _inventory.SetCount(new DenominationKey(500m, CurrencyCashType.Coin, "JPY"), 20);

        _hardwareStatusManager = new HardwareStatusManager();
        _hardwareStatusManager.SetConnected(true);
        var manager = new CashChangerManager(_inventory, new TransactionHistory(), new ChangeCalculator());
        _depositController = new DepositController(_inventory, _hardwareStatusManager);
        _dispenseController = new DispenseController(manager, _hardwareStatusManager, null);

        _facade = new UposDispenseFacade(
            _dispenseController,
            _depositController,
            _hardwareStatusManager,
            _inventory,
            new Mock<ILogger>().Object);
    }

    /// <summary>入金中に出金しようとすると例外がスローされることを確認します。</summary>
    [Fact]
    public void DispenseByAmount_WhenDepositInProgress_ShouldThrow()
    {
        _depositController.BeginDeposit();

        Assert.Throws<PosControlException>(() =>
            _facade.DispenseByAmount(1000, "JPY", 1m, false, (_, _, _) => { }));
    }

    /// <summary>ジャム中に出金しようとすると例外がスローされることを確認します。</summary>
    [Fact]
    public void DispenseByAmount_WhenJammed_ShouldThrow()
    {
        _hardwareStatusManager.SetJammed(true);

        Assert.Throws<PosControlException>(() =>
            _facade.DispenseByAmount(1000, "JPY", 1m, false, (_, _, _) => { }));
    }

    /// <summary>金額0以下で例外がスローされることを確認します。</summary>
    [Fact]
    public void DispenseByAmount_ZeroAmount_ShouldThrow()
    {
        Assert.Throws<PosControlException>(() =>
            _facade.DispenseByAmount(0, "JPY", 1m, false, (_, _, _) => { }));
    }

    /// <summary>正常な金額出金が成功することを確認します。</summary>
    [Fact]
    public void DispenseByAmount_ValidAmount_ShouldSucceed()
    {
        ErrorCode? resultCode = null;
        _facade.DispenseByAmount(1000, "JPY", 1m, false, (code, _, _) => resultCode = code);

        Assert.Equal(ErrorCode.Success, resultCode);
    }

    /// <summary>金種指定の出金で在庫不足時に例外がスローされることを確認します。</summary>
    [Fact]
    public void DispenseByCashCounts_InsufficientInventory_ShouldThrow()
    {
        var cashCounts = new[] { new CashCount(CashCountType.Bill, 1000, 999) };

        Assert.Throws<PosControlException>(() =>
            _facade.DispenseByCashCounts(cashCounts, "JPY", 1m, false, (_, _, _) => { }));
    }
}
