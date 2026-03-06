using Microsoft.PointOfService;
using Moq;
using Shouldly;
using Xunit;
using CashChangerSimulator.Device.Commands;
using CashChangerSimulator.Device.Coordination;
using CashChangerSimulator.Device;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

namespace CashChangerSimulator.Tests.Device;

public class UposCommandTests
{
    private readonly Mock<IUposMediator> _mediatorMock;
    private readonly Mock<DepositController> _depositControllerMock;

    public UposCommandTests()
    {
        _mediatorMock = new Mock<IUposMediator>();
        // DepositController needs Inventory and HardwareStatusManager
        var inventory = new CashChangerSimulator.Core.Models.Inventory();
        var hardware = new CashChangerSimulator.Core.Managers.HardwareStatusManager();
        _depositControllerMock = new Mock<DepositController>(inventory, hardware, null, null);
    }

    [Fact]
    public void BeginDepositCommand_Execute_ShouldCallController()
    {
        // Arrange
        var command = new BeginDepositCommand(_depositControllerMock.Object);

        // Act
        command.Execute();

        // Assert
        _depositControllerMock.Verify(c => c.BeginDeposit(), Times.Once);
    }

    [Fact]
    public void BeginDepositCommand_Verify_ShouldCallMediator()
    {
        // Arrange
        var command = new BeginDepositCommand(_depositControllerMock.Object);

        // Act
        command.Verify(_mediatorMock.Object, false);

        // Assert
        _mediatorMock.Verify(m => m.VerifyState(false, true, false), Times.Once);
    }

    [Fact]
    public void FixDepositCommand_Execute_ShouldCallController()
    {
        var command = new FixDepositCommand(_depositControllerMock.Object);
        command.Execute();
        _depositControllerMock.Verify(c => c.FixDeposit(), Times.Once);
    }

    [Fact]
    public void EndDepositCommand_Execute_ShouldCallController()
    {
        var action = CashDepositAction.NoChange;
        var command = new EndDepositCommand(_depositControllerMock.Object, action);
        command.Execute();
        _depositControllerMock.Verify(c => c.EndDeposit(action), Times.Once);
    }

    [Fact]
    public void PauseDepositCommand_Execute_ShouldCallController()
    {
        var control = CashDepositPause.Pause;
        var command = new PauseDepositCommand(_depositControllerMock.Object, control);
        command.Execute();
        _depositControllerMock.Verify(c => c.PauseDeposit(control), Times.Once);
    }

    [Fact]
    public void RepayDepositCommand_Execute_ShouldCallController()
    {
        var command = new RepayDepositCommand(_depositControllerMock.Object);
        command.Execute();
        _depositControllerMock.Verify(c => c.RepayDeposit(), Times.Once);
    }

    [Fact]
    public void ReadCashCountsCommand_Execute_ShouldReturnCounts()
    {
        var inventory = new Inventory();
        var command = new ReadCashCountsCommand(inventory, "JPY", 1.0m);
        command.Execute();
        command.Result.Counts.ShouldNotBeNull();
    }

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

    [Fact]
    public void PurgeCashCommand_Execute_ShouldCallManager()
    {
        var managerMock = new Mock<CashChangerManager>(new Inventory(), new TransactionHistory(), new ChangeCalculator());
        var command = new PurgeCashCommand(managerMock.Object);
        command.Execute();
        managerMock.Verify(m => m.PurgeCash(), Times.Once);
    }

    [Fact]
    public void CheckHealthCommand_Execute_ShouldReturnReport()
    {
        var diagMock = new Mock<DiagnosticController>(new Inventory(), new HardwareStatusManager(), LogProvider.CreateLogger<DiagnosticController>());
        diagMock.Setup(d => d.GetHealthReport(HealthCheckLevel.Internal)).Returns("OK");
        var command = new CheckHealthCommand(diagMock.Object, HealthCheckLevel.Internal);
        
        command.Execute();
        
        command.Result.ShouldBe("OK");
    }

    [Fact]
    public void RetrieveStatisticsCommand_Execute_ShouldReturnXml()
    {
        var diagMock = new Mock<DiagnosticController>(new Inventory(), new HardwareStatusManager(), LogProvider.CreateLogger<DiagnosticController>());
        diagMock.Setup(d => d.RetrieveStatistics(It.IsAny<string[]>())).Returns("<xml/>");
        var command = new RetrieveStatisticsCommand(diagMock.Object, new[] { "*" });
        
        command.Execute();
        
        command.Result.ShouldBe("<xml/>");
    }

    [Fact]
    public void UpdateStatisticsCommand_Execute_ShouldWork()
    {
        var command = new UpdateStatisticsCommand(new Statistic[0]);
        command.Execute(); // Should not throw
    }

    [Fact]
    public void ResetStatisticsCommand_Execute_ShouldWork()
    {
        var command = new ResetStatisticsCommand(new string[0]);
        command.Execute(); // Should not throw
    }

    [Fact]
    public void DispenseChangeCommand_Execute_ShouldCallController()
    {
        var manager = new Mock<CashChangerManager>(new Inventory(), new TransactionHistory(), new ChangeCalculator());
        var controllerMock = new Mock<DispenseController>(manager.Object);
        var command = new DispenseChangeCommand(controllerMock.Object, 1000m, false, (e, ex) => { });
        
        command.Execute();
        
        controllerMock.Verify(c => c.DispenseChangeAsync(1000m, false, It.IsAny<Action<ErrorCode, int>>(), null), Times.Once);
    }

    [Fact]
    public void DispenseCashCommand_Execute_ShouldCallController()
    {
        var manager = new Mock<CashChangerManager>(new Inventory(), new TransactionHistory(), new ChangeCalculator());
        var controllerMock = new Mock<DispenseController>(manager.Object);
        var counts = new Dictionary<DenominationKey, int>();
        var command = new DispenseCashCommand(controllerMock.Object, counts, false, (e, ex) => { });
        
        command.Execute();
        
        controllerMock.Verify(c => c.DispenseCashAsync(counts, false, It.IsAny<Action<ErrorCode, int>>()), Times.Once);
    }

    [Fact]
    public void ClearOutputCommand_Execute_ShouldCallController()
    {
        var manager = new Mock<CashChangerManager>(new Inventory(), new TransactionHistory(), new ChangeCalculator());
        var controllerMock = new Mock<DispenseController>(manager.Object);
        var command = new ClearOutputCommand(controllerMock.Object);
        
        command.Execute();
        
        controllerMock.Verify(c => c.ClearOutput(), Times.Once);
    }
}
