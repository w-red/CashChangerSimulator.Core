using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>UposDispenseFacade の動作を検証するテストクラス。</summary>
public class UposDispenseFacadeTest
{
    private readonly Inventory _inventory;
    private readonly DepositController _depositController;
    private readonly DispenseController _dispenseController;
    private readonly HardwareStatusManager _hardwareStatusManager;
    private readonly UposDispenseFacade _facade;

    /// <summary>UposDispenseFacadeTest の新しいインスタンスを初期化します。</summary>
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
    public void DispenseByAmountWhenDepositInProgressShouldThrow()
    {
        _depositController.BeginDeposit();

        Should.Throw<PosControlException>(() =>
            _facade.DispenseByAmount(1000, "JPY", 1m, false, (_, _, _) => { }));
    }

    /// <summary>ジャム中に出金しようとすると例外がスローされることを確認します。</summary>
    [Fact]
    public void DispenseByAmountWhenJammedShouldThrow()
    {
        _hardwareStatusManager.SetJammed(true);

        Should.Throw<PosControlException>(() =>
            _facade.DispenseByAmount(1000, "JPY", 1m, false, (_, _, _) => { }));
    }

    /// <summary>金額0以下で例外がスローされることを確認します。</summary>
    [Fact]
    public void DispenseByAmountZeroAmountShouldThrow()
    {
        Should.Throw<PosControlException>(() =>
            _facade.DispenseByAmount(0, "JPY", 1m, false, (_, _, _) => { }));
    }

    /// <summary>正常な金額出金が成功することを確認します。</summary>
    [Fact]
    public void DispenseByAmountValidAmountShouldSucceed()
    {
        ErrorCode? resultCode = null;
        _facade.DispenseByAmount(1000, "JPY", 1m, false, (code, _, _) => resultCode = code);

        resultCode.ShouldBe(ErrorCode.Success);
    }

    /// <summary>金種指定の出金で在庫不足時に例外がスローされることを確認します。</summary>
    [Fact]
    public void DispenseByCashCountsInsufficientInventoryShouldThrow()
    {
        var cashCounts = new[] { new CashCount(CashCountType.Bill, 1000, 999) };

        Should.Throw<PosControlException>(() =>
            _facade.DispenseByCashCounts(cashCounts, "JPY", 1m, false, (_, _, _) => { }));
    }
}
