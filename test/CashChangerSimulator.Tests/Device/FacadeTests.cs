using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Coordination;
using CashChangerSimulator.Device.Facades;
using CashChangerSimulator.Device.Commands;
using Microsoft.PointOfService;
using Moq;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Configuration;

namespace CashChangerSimulator.Tests.Device;

/// <summary>各機能ファサード（DepositFacade, InventoryFacade等）がメディエータを介してコマンドを正しく発行することを検証するテストクラス。</summary>
public class FacadeTests
{
    private readonly Mock<IUposMediator> _mediatorMock;
    private readonly Mock<DepositController> DepositControllerMock;
    private readonly Mock<Inventory> InventoryMock;
    private readonly Mock<CashChangerManager> _managerMock;
    private readonly Mock<DiagnosticController> _diagnosticControllerMock;

    public FacadeTests()
    {
        _mediatorMock = new Mock<IUposMediator>();
        
        var inventory = new Inventory();
        var hardwareStatusManager = new HardwareStatusManager();
        
        // Mocking classes with dependencies, providing real objects for required parameters
        DepositControllerMock = new Mock<DepositController>(
            inventory, 
            hardwareStatusManager, 
            _managerMock?.Object ?? new Mock<CashChangerManager>(inventory, new Mock<TransactionHistory>().Object, new ChangeCalculator(), new ConfigurationProvider()).Object,
            new ConfigurationProvider());
        InventoryMock = new Mock<Inventory>();
        _managerMock = new Mock<CashChangerManager>(
            inventory, 
            new Mock<TransactionHistory>().Object, 
            new ChangeCalculator(), 
            new ConfigurationProvider());
        _diagnosticControllerMock = new Mock<DiagnosticController>(inventory, hardwareStatusManager);
    }

    /// <summary>DepositFacade.BeginDeposit が対応するプロトコルコマンドを実行することを検証します。</summary>
    [Fact]
    public void DepositFacadeBeginDepositShouldExecuteCommand()
    {
        // Arrange
        var facade = new DepositFacade(DepositControllerMock.Object, _mediatorMock.Object);

        // Act
        facade.BeginDeposit();

        // Assert
        _mediatorMock.Verify(m => m.Execute(It.IsAny<BeginDepositCommand>()), Times.Once);
    }

    /// <summary>InventoryFacade.ReadCashCounts が対応するプロトコルコマンドを実行することを検証します。</summary>
    [Fact]
    public void InventoryFacadeReadCashCountsShouldExecuteCommand()
    {
        // Arrange
        var facade = new InventoryFacade(InventoryMock.Object, _managerMock.Object, _mediatorMock.Object);

        // Act
        facade.ReadCashCounts("JPY", 1.0m);

        // Assert
        _mediatorMock.Verify(m => m.Execute(It.IsAny<ReadCashCountsCommand>()), Times.Once);
    }

    /// <summary>DiagnosticsFacade.CheckHealth が対応するプロトコルコマンドを実行することを検証します。</summary>
    [Fact]
    public void DiagnosticsFacadeCheckHealthShouldExecuteCommand()
    {
        // Arrange
        var facade = new DiagnosticsFacade(_diagnosticControllerMock.Object, _mediatorMock.Object);

        // Act
        facade.CheckHealth(HealthCheckLevel.Internal);

        // Assert
        _mediatorMock.Verify(m => m.Execute(It.IsAny<CheckHealthCommand>()), Times.Once);
    }
}
