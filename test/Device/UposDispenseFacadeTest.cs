using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Device.PosForDotNet;
using CashChangerSimulator.Device.PosForDotNet.Coordination;
using CashChangerSimulator.Device.Virtual;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PointOfService;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>UposDispenseFacade の動作を検証するテストクラス。</summary>
public class UposDispenseFacadeTest
{
    private readonly Inventory inventory;
    private readonly DepositController depositController;
    private readonly DispenseController dispenseController;
    private readonly HardwareStatusManager hardwareStatusManager;
    private readonly Mock<IUposMediator> mediatorMock;
    private readonly UposDispenseFacade facade;

    /// <summary>Initializes a new instance of the <see cref="UposDispenseFacadeTest"/> class.UposDispenseFacadeTest の新しいインスタンスを初期化します。</summary>
    public UposDispenseFacadeTest()
    {
        inventory = Inventory.Create();
        inventory.SetCount(new DenominationKey(1000m, CurrencyCashType.Bill, "JPY"), 10);
        inventory.SetCount(new DenominationKey(500m, CurrencyCashType.Coin, "JPY"), 20);

        hardwareStatusManager = HardwareStatusManager.Create();
        hardwareStatusManager.SetConnected(true);
        var manager = new CashChangerManager(inventory, new TransactionHistory(), null);
        depositController = new DepositController(inventory, hardwareStatusManager);
        var timeProvider = new FakeTimeProvider();
        dispenseController = new DispenseController(manager, inventory, new ConfigurationProvider(), NullLoggerFactory.Instance, hardwareStatusManager, new Mock<IDeviceSimulator>().Object, timeProvider);
        mediatorMock = new Mock<IUposMediator>();
        mediatorMock.Setup(m => m.Execute(It.IsAny<IUposCommand>()))
            .Callback<IUposCommand>((cmd) => cmd.Execute());

        facade = new UposDispenseFacade(
            dispenseController,
            depositController,
            hardwareStatusManager,
            inventory,
            mediatorMock.Object,
            new Mock<ILogger<UposDispenseFacade>>().Object);
    }

    private void SetupMediatorToThrow()
    {
        mediatorMock.Setup(m => m.Execute(It.IsAny<IUposCommand>()))
            .Callback<IUposCommand>(cmd =>
            {
                cmd.Verify(mediatorMock.Object);
                cmd.Execute();
            });
    }

    /// <summary>入金中に出金しようとすると例外がスローされることを確認します。</summary>
    [Fact]
    public void DispenseByAmountWhenDepositInProgressShouldThrow()
    {
        depositController.BeginDeposit();
        SetupMediatorToThrow();

        Should.Throw<PosControlException>(() =>
            facade.DispenseByAmount(1000, "JPY", 1m, false));
    }

    /// <summary>ジャム中に出金しようとすると例外がスローされることを確認します。</summary>
    [Fact]
    public void DispenseByAmountWhenJammedShouldThrow()
    {
        hardwareStatusManager.SetJammed(true);
        SetupMediatorToThrow();

        Should.Throw<PosControlException>(() =>
            facade.DispenseByAmount(1000, "JPY", 1m, false));
    }

    /// <summary>金額0以下で例外がスローされることを確認します。</summary>
    [Fact]
    public void DispenseByAmountZeroAmountShouldThrow()
    {
        Should.Throw<PosControlException>(() =>
            facade.DispenseByAmount(0, "JPY", 1m, false));
    }

    /// <summary>正常な金額出金が成功することを確認します。</summary>
    [Fact]
    public void DispenseByAmountValidAmountShouldSucceed()
    {
        facade.DispenseByAmount(1000, "JPY", 1m, false);
        dispenseController.LastErrorCode.ShouldBe(DeviceErrorCode.Success);
    }

    /// <summary>金種指定の出金で在庫不足時に例外がスローされることを確認します。</summary>
    [Fact]
    public void DispenseByCashCountsInsufficientInventoryShouldThrow()
    {
        var cashCounts = new[] { new CashCount(CashCountType.Bill, 1000, 999) };
        SetupMediatorToThrow();

        Should.Throw<PosControlException>(() =>
            facade.DispenseByCashCounts(cashCounts, "JPY", 1m, false));
    }
}
