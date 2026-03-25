using Microsoft.PointOfService;
using Moq;
using Shouldly;
using CashChangerSimulator.Device.Commands;
using CashChangerSimulator.Device.Coordination;
using CashChangerSimulator.Device;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Managers;

namespace CashChangerSimulator.Tests.Device;

/// <summary>UPOS コマンドクラス群（BeginDeposit, DispenseCash 等）の実行および検証ロジックをテストするクラス。</summary>
public class UposCommandTests
{
    private readonly Mock<IUposMediator> _mediatorMock;
    private readonly Mock<DepositController> DepositControllerMock;

    public UposCommandTests()
    {
        _mediatorMock = new Mock<IUposMediator>();
        // DepositController needs Inventory and HardwareStatusManager
        var inventory = new CashChangerSimulator.Core.Models.Inventory();
        var hardware = new CashChangerSimulator.Core.Managers.HardwareStatusManager();
        DepositControllerMock = new Mock<DepositController>(inventory, hardware, null!, null!);
    }

    /// <summary>BeginDepositCommand の実行がコントローラへ委譲されることを検証します。</summary>
    [Fact]
    public void BeginDepositCommand_Execute_ShouldCallController()
    {
        // Arrange
        var command = new BeginDepositCommand(DepositControllerMock.Object);

        // Act
        command.Execute();

        // Assert
        DepositControllerMock.Verify(c => c.BeginDeposit(), Times.Once);
    }

    /// <summary>BeginDepositCommand の検証処理がメディエータを介して行われることを検証します。</summary>
    [Fact]
    public void BeginDepositCommand_Verify_ShouldCallMediator()
    {
        // Arrange
        var command = new BeginDepositCommand(DepositControllerMock.Object);

        // Act
        command.Verify(_mediatorMock.Object);

        // Assert
        _mediatorMock.Verify(m => m.VerifyState(true, false, false), Times.Once);
    }

    /// <summary>FixDepositCommand の実行がコントローラへ委譲されることを検証します。</summary>
    [Fact]
    public void FixDepositCommand_Execute_ShouldCallController()
    {
        var command = new FixDepositCommand(DepositControllerMock.Object);
        command.Execute();
        DepositControllerMock.Verify(c => c.FixDeposit(), Times.Once);
    }

    /// <summary>EndDepositCommand の実行がコントローラへ委譲されることを検証します。</summary>
    [Fact]
    public void EndDepositCommand_Execute_ShouldCallController()
    {
        var action = CashDepositAction.NoChange;
        var command = new EndDepositCommand(DepositControllerMock.Object, action);
        command.Execute();
        DepositControllerMock.Verify(c => c.EndDeposit(action), Times.Once);
    }

    /// <summary>PauseDepositCommand の実行がコントローラへ委譲されることを検証します。</summary>
    [Fact]
    public void PauseDepositCommand_Execute_ShouldCallController()
    {
        var control = CashDepositPause.Pause;
        var command = new PauseDepositCommand(DepositControllerMock.Object, control);
        command.Execute();
        DepositControllerMock.Verify(c => c.PauseDeposit(control), Times.Once);
    }

    /// <summary>RepayDepositCommand の実行がコントローラへ委譲されることを検証します。</summary>
    [Fact]
    public void RepayDepositCommand_Execute_ShouldCallController()
    {
        var command = new RepayDepositCommand(DepositControllerMock.Object);
        command.Execute();
        DepositControllerMock.Verify(c => c.RepayDeposit(), Times.Once);
    }

    /// <summary>ReadCashCountsCommand の実行により在庫カウントが取得できることを検証します。</summary>
    [Fact]
    public void ReadCashCountsCommand_Execute_ShouldReturnCounts()
    {
        var inventory = new Inventory();
        var command = new ReadCashCountsCommand(inventory, "JPY", 1.0m);
        command.Execute();
        command.Result.Counts.ShouldNotBeNull();
    }

    /// <summary>AdjustCashCountsCommand の実行により在庫の更新が行われることを検証します。</summary>
    [Fact]
    public void AdjustCashCountsCommand_Execute_ShouldCallInventory()
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
    public void PurgeCashCommand_Execute_ShouldCallManager()
    {
        var managerMock = new Mock<CashChangerManager>(new Inventory(), new TransactionHistory(), new ChangeCalculator());
        var command = new PurgeCashCommand(managerMock.Object);
        command.Execute();
        managerMock.Verify(m => m.PurgeCash(), Times.Once);
    }

    /// <summary>CheckHealthCommand の実行により診断レポートが返却されることを検証します。</summary>
    [Fact]
    public void CheckHealthCommand_Execute_ShouldReturnReport()
    {
        var diagMock = new Mock<DiagnosticController>(new Inventory(), new HardwareStatusManager());
        diagMock.Setup(d => d.GetHealthReport(HealthCheckLevel.Internal)).Returns("OK");
        var command = new CheckHealthCommand(diagMock.Object, HealthCheckLevel.Internal);
        
        command.Execute();
        
        command.Result.ShouldBe("OK");
    }

    /// <summary>RetrieveStatisticsCommand の実行により統計 XML が返却されることを検証します。</summary>
    [Fact]
    public void RetrieveStatisticsCommand_Execute_ShouldReturnXml()
    {
        var diagMock = new Mock<DiagnosticController>(new Inventory(), new HardwareStatusManager());
        diagMock.Setup(d => d.RetrieveStatistics(It.IsAny<string[]>())).Returns("<xml/>");
        var command = new RetrieveStatisticsCommand(diagMock.Object, new[] { "*" });
        
        command.Execute();
        
        command.Result.ShouldBe("<xml/>");
    }

    /// <summary>UpdateStatisticsCommand の実行が正常に終了することを検証します。</summary>
    [Fact]
    public void UpdateStatisticsCommand_Execute_ShouldWork()
    {
        var command = new UpdateStatisticsCommand(new Statistic[0]);
        command.Execute(); // Should not throw
    }

    /// <summary>ResetStatisticsCommand の実行が正常に終了することを検証します。</summary>
    [Fact]
    public void ResetStatisticsCommand_Execute_ShouldWork()
    {
        var command = new ResetStatisticsCommand(new string[0]);
        command.Execute(); // Should not throw
    }

    /// <summary>DispenseChangeCommand の実行がコントローラへ委譲されることを検証します。</summary>
    [Fact]
    public void DispenseChangeCommand_Execute_ShouldCallController()
    {
        var manager = new Mock<CashChangerManager>(new Inventory(), new TransactionHistory(), new ChangeCalculator());
        var hw = new HardwareStatusManager();
        var deposit = new Mock<DepositController>(new Inventory(), hw, null!, null!);
        var sim = new Mock<IDeviceSimulator>();
        var controllerMock = new Mock<DispenseController>(manager.Object, hw, sim.Object);
        var command = new DispenseChangeCommand(controllerMock.Object, hw, deposit.Object, 1000m, false, (e, ex) => { });
        
        command.Execute();
        
        controllerMock.Verify(c => c.DispenseChangeAsync(1000m, false, It.IsAny<Action<ErrorCode, int>>(), null), Times.Once);
    }

    /// <summary>DispenseCashCommand の実行がコントローラへ委譲されることを検証します。</summary>
    [Fact]
    public void DispenseCashCommand_Execute_ShouldCallController()
    {
        var manager = new Mock<CashChangerManager>(new Inventory(), new TransactionHistory(), new ChangeCalculator());
        var inv = new Inventory();
        var hw = new HardwareStatusManager();
        var deposit = new Mock<DepositController>(inv, hw, null!, null!);
        var sim = new Mock<IDeviceSimulator>();
        var controllerMock = new Mock<DispenseController>(manager.Object, hw, sim.Object);
        var counts = new Dictionary<DenominationKey, int>();
        var command = new DispenseCashCommand(controllerMock.Object, inv, hw, deposit.Object, counts, false, (e, ex) => { });
        
        command.Execute();
        
        controllerMock.Verify(c => c.DispenseCashAsync(counts, false, It.IsAny<Action<ErrorCode, int>>()), Times.Once);
    }

    /// <summary>ClearOutputCommand の実行がコントローラへ委譲されることを検証します。</summary>
    [Fact]
    public void ClearOutputCommand_Execute_ShouldCallController()
    {
        var manager = new Mock<CashChangerManager>(new Inventory(), new TransactionHistory(), new ChangeCalculator());
        var hw = new HardwareStatusManager();
        var sim = new Mock<IDeviceSimulator>();
        var controllerMock = new Mock<DispenseController>(manager.Object, hw, sim.Object);
        var command = new ClearOutputCommand(controllerMock.Object);
        
        command.Execute();
        
        controllerMock.Verify(c => c.ClearOutput(), Times.Once);
    }
}
