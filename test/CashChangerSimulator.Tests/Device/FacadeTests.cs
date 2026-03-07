using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Coordination;
using CashChangerSimulator.Device.Facades;
using CashChangerSimulator.Device.Commands;
using Microsoft.PointOfService;
using Moq;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Tests.Device;

public class FacadeTests
{
    private readonly Mock<IUposMediator> _mediatorMock;
    private readonly Mock<DepositController> _depositControllerMock;
    private readonly Mock<Inventory> _inventoryMock;
    private readonly Mock<CashChangerManager> _managerMock;
    private readonly Mock<DiagnosticController> _diagnosticControllerMock;

    public FacadeTests()
    {
        _mediatorMock = new Mock<IUposMediator>();
        
        var inventory = new Inventory();
        var hardwareStatusManager = new HardwareStatusManager();
        
        // Mocking classes with dependencies, providing real objects for required parameters
        _depositControllerMock = new Mock<DepositController>(inventory, hardwareStatusManager, null, null);
        _inventoryMock = new Mock<Inventory>();
        _managerMock = new Mock<CashChangerManager>(inventory, null, null, null);
        _diagnosticControllerMock = new Mock<DiagnosticController>(inventory, hardwareStatusManager);
    }

    [Fact]
    public void DepositFacade_BeginDeposit_ShouldExecuteCommand()
    {
        // Arrange
        var facade = new DepositFacade(_depositControllerMock.Object, _mediatorMock.Object);

        // Act
        facade.BeginDeposit(skipStateVerification: true);

        // Assert
        _mediatorMock.Verify(m => m.Execute(It.IsAny<BeginDepositCommand>(), true), Times.Once);
    }

    [Fact]
    public void InventoryFacade_ReadCashCounts_ShouldExecuteCommand()
    {
        // Arrange
        var facade = new InventoryFacade(_inventoryMock.Object, _managerMock.Object, _mediatorMock.Object);

        // Act
        facade.ReadCashCounts("JPY", 1.0m, skipStateVerification: false);

        // Assert
        _mediatorMock.Verify(m => m.Execute(It.IsAny<ReadCashCountsCommand>(), false), Times.Once);
    }

    [Fact]
    public void DiagnosticsFacade_CheckHealth_ShouldExecuteCommand()
    {
        // Arrange
        var facade = new DiagnosticsFacade(_diagnosticControllerMock.Object, _mediatorMock.Object);

        // Act
        facade.CheckHealth(HealthCheckLevel.Internal, skipStateVerification: true);

        // Assert
        _mediatorMock.Verify(m => m.Execute(It.IsAny<CheckHealthCommand>(), true), Times.Once);
    }
}
