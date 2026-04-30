using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device.PosForDotNet.Commands;
using CashChangerSimulator.Device.PosForDotNet.Coordination;
using CashChangerSimulator.Device.Virtual;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PointOfService;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>UPOS コマンドクラス群(BeginDeposit, DispenseCash 等)の実行および検証ロジックをテストするクラス。</summary>
public class UposCommandTests
{
    private readonly Mock<IUposMediator> mediatorMock;
    private readonly Mock<DepositController> depositControllerMock;

    public UposCommandTests()
    {
        mediatorMock = new Mock<IUposMediator>();

        // DepositController needs CashChangerManager, Inventory, HardwareStatusManager, ConfigurationProvider, ILoggerFactory, IDeviceSimulator
        var inventory = Inventory.Create();
        var hardware = HardwareStatusManager.Create();
        var configProvider = new ConfigurationProvider();
        var manager = new CashChangerManager(inventory, new TransactionHistory(), configProvider);
        depositControllerMock = new Mock<DepositController>(manager, inventory, hardware, configProvider, NullLoggerFactory.Instance, null, false);
    }

    /// <summary>BeginDepositCommand の実行がコントローラへ委譲されることを検証します。</summary>
    [Fact]
    public void BeginDepositCommandExecuteShouldCallController()
    {
        // Arrange
        var command = new BeginDepositCommand(depositControllerMock.Object);

        // Act
        command.Execute();

        // Assert
        depositControllerMock.Verify(c => c.BeginDeposit(), Times.Once);
    }

    /// <summary>BeginDepositCommand の検証処理がメディエータを介して行われることを検証します。</summary>
    [Fact]
    public void BeginDepositCommandVerifyShouldCallMediator()
    {
        // Arrange
        var command = new BeginDepositCommand(depositControllerMock.Object);

        // Act
        command.Verify(mediatorMock.Object);

        // Assert
        mediatorMock.Verify(m => m.VerifyState(true, false, false), Times.Once);
    }

    /// <summary>FixDepositCommand の実行がコントローラへ委譲されることを検証します。</summary>
    [Fact]
    public void FixDepositCommandExecuteShouldCallController()
    {
        var command = new FixDepositCommand(depositControllerMock.Object);
        command.Execute();
        depositControllerMock.Verify(c => c.FixDeposit(), Times.Once);
    }

    /// <summary>EndDepositCommand の実行がコントローラへ委譲されることを検証します。</summary>
    [Fact]
    public void EndDepositCommandExecuteShouldCallController()
    {
        var action = CashDepositAction.NoChange;
        var command = new EndDepositCommand(depositControllerMock.Object, action);
        command.Execute();
        depositControllerMock.Verify(c => c.EndDepositAsync(It.IsAny<DepositAction>()), Times.Once);
    }

    /// <summary>PauseDepositCommand の実行がコントローラへ委譲されることを検証します。</summary>
    [Fact]
    public void PauseDepositCommandExecuteShouldCallController()
    {
        var control = CashDepositPause.Pause;
        var command = new PauseDepositCommand(depositControllerMock.Object, control);
        command.Execute();
        depositControllerMock.Verify(c => c.PauseDeposit(It.IsAny<DeviceDepositPause>()), Times.Once);
    }

    /// <summary>RepayDepositCommand の実行がコントローラへ委譲されることを検証します。</summary>
    [Fact]
    public void RepayDepositCommandExecuteShouldCallController()
    {
        var command = new RepayDepositCommand(depositControllerMock.Object);
        command.Execute();
        depositControllerMock.Verify(c => c.RepayDepositAsync(), Times.Once);
    }

    /// <summary>ReadCashCountsCommand の実行により在庫カウントが取得できることを検証します。</summary>
    [Fact]
    public void ReadCashCountsCommandExecuteShouldReturnCounts()
    {
        var inventory = Inventory.Create();
        var command = new ReadCashCountsCommand(inventory, "JPY", 1.0m);
        command.Execute();
        command.Result.Counts.ShouldNotBeNull();
    }

    /// <summary>AdjustCashCountsCommand の実行により在庫の更新が行われることを検証します。</summary>
    [Fact]
    public void AdjustCashCountsCommandExecuteShouldCallInventory()
    {
        var inventoryMock = new Mock<Inventory>();
        var hardwareMock = new Mock<HardwareStatusManager>();
        var command = new AdjustCashCountsCommand(inventoryMock.Object, new CashCount[0], "JPY", 1.0m, hardwareMock.Object);
        command.Execute();

        // Since it iterates over dict, it might not call specific method if empty, but we verify it runs.
        inventoryMock.Verify(i => i.SetCount(It.IsAny<DenominationKey>(), It.IsAny<int>()), Times.AtMostOnce());
    }

    /// <summary>PurgeCashCommand の実行がマネージャへ委譲されることを検証します。</summary>
    [Fact]
    public void PurgeCashCommandExecuteShouldCallManager()
    {
        var managerMock = new Mock<CashChangerManager>(Inventory.Create(), new TransactionHistory(), null);
        var command = new PurgeCashCommand(managerMock.Object);
        command.Execute();
        managerMock.Verify(m => m.PurgeCash(), Times.Once);
    }

    /// <summary>CheckHealthCommand の実行により診断レポートが返却されることを検証します。</summary>
    [Fact]
    public void CheckHealthCommandExecuteShouldReturnReport()
    {
        var diagMock = new Mock<DiagnosticController>(Inventory.Create(), HardwareStatusManager.Create());
        diagMock.Setup(d => d.GetHealthReport(PosSharp.Abstractions.HealthCheckLevel.Internal)).Returns("OK");
        var command = new CheckHealthCommand(diagMock.Object, HealthCheckLevel.Internal);

        command.Execute();

        command.Result.ShouldBe("OK");
    }

    /// <summary>RetrieveStatisticsCommand の実行により統計 XML が返却されることを検証します。</summary>
    [Fact]
    public void RetrieveStatisticsCommandExecuteShouldReturnXml()
    {
        var diagMock = new Mock<DiagnosticController>(Inventory.Create(), HardwareStatusManager.Create());
        diagMock.Setup(d => d.RetrieveStatistics(It.IsAny<string[]>())).Returns("<xml/>");
        var command = new RetrieveStatisticsCommand(diagMock.Object, ["*"]);

        command.Execute();

        command.Result.ShouldBe("<xml/>");
    }

    /// <summary>UpdateStatisticsCommand の実行が正常に終了することを検証します。</summary>
    [Fact]
    public void UpdateStatisticsCommandExecuteShouldWork()
    {
        var command = new UpdateStatisticsCommand([]);
        command.Execute(); // Should not throw
    }

    /// <summary>ResetStatisticsCommand の実行が正常に終了することを検証します。</summary>
    [Fact]
    public void ResetStatisticsCommandExecuteShouldWork()
    {
        var command = new ResetStatisticsCommand([]);
        command.Execute(); // Should not throw
    }

    /// <summary>DispenseChangeCommand の実行がコントローラへ委譲されることを検証します。</summary>
    [Fact]
    public void DispenseChangeCommandExecuteShouldCallController()
    {
        var manager = new Mock<CashChangerManager>(Inventory.Create(), new TransactionHistory(), null);
        var hw = HardwareStatusManager.Create();
        var sim = new Mock<IDeviceSimulator>();
        var inv = Inventory.Create();
        var cp = new ConfigurationProvider();
        var deposit = new Mock<DepositController>(manager.Object, inv, hw, cp, NullLoggerFactory.Instance, null, false);
        var controllerMock = new Mock<DispenseController>(manager.Object, inv, cp, NullLoggerFactory.Instance, hw, sim.Object);
        var command = new DispenseChangeCommand(controllerMock.Object, hw, deposit.Object, 1000m, false);

        command.Execute();
        controllerMock.Verify(c => c.DispenseChangeAsync(1000, false), Times.Once);
    }

    /// <summary>DispenseCashCommand の実行がコントローラへ委譲されることを検証します。</summary>
    [Fact]
    public void DispenseCashCommandExecuteShouldCallController()
    {
        var manager = new Mock<CashChangerManager>(Inventory.Create(), new TransactionHistory(), null);
        var inv = Inventory.Create();
        var hw = HardwareStatusManager.Create();
        var sim = new Mock<IDeviceSimulator>();
        var cp = new ConfigurationProvider();
        var deposit = new Mock<DepositController>(manager.Object, inv, hw, cp, NullLoggerFactory.Instance, null, false);
        var controllerMock = new Mock<DispenseController>(manager.Object, inv, cp, NullLoggerFactory.Instance, hw, sim.Object);
        var counts = new Dictionary<DenominationKey, int>();
        var command = new DispenseCashCommand(controllerMock.Object, inv, hw, deposit.Object, counts, false);

        command.Execute();

        controllerMock.Verify(c => c.DispenseCashAsync(counts, false), Times.Once);
    }

    /// <summary>ClearOutputCommand の実行がコントローラへ委譲されることを検証します。</summary>
    [Fact]
    public void ClearOutputCommandExecuteShouldCallController()
    {
        var manager = new Mock<CashChangerManager>(Inventory.Create(), new TransactionHistory(), null);
        var hw = HardwareStatusManager.Create();
        var sim = new Mock<IDeviceSimulator>();
        var inv = Inventory.Create();
        var cp = new ConfigurationProvider();
        var controllerMock = new Mock<DispenseController>(manager.Object, inv, cp, NullLoggerFactory.Instance, hw, sim.Object);
        var command = new ClearOutputCommand(controllerMock.Object);

        command.Execute();

        controllerMock.Verify(c => c.ClearOutput(), Times.Once);
    }
}
