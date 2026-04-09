using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device.PosForDotNet.Commands;
using CashChangerSimulator.Device.PosForDotNet.Coordination;
using CashChangerSimulator.Device.PosForDotNet.Facades;
using CashChangerSimulator.Device.Virtual;
using Microsoft.PointOfService;
using Moq;

namespace CashChangerSimulator.Tests.Device;

/// <summary>各機能ファサード（DepositFacade, InventoryFacade等）がメディエータを介してコマンドを正しく発行することを検証するテストクラス。</summary>
public class FacadeTests
{
    private readonly Mock<IUposMediator> mediatorMock;
    private readonly Mock<DepositController> depositControllerMock;
    private readonly Mock<Inventory> inventoryMock;
    private readonly Mock<CashChangerManager> managerMock;
    private readonly Mock<DiagnosticController> diagnosticControllerMock;

    public FacadeTests()
    {
        mediatorMock = new Mock<IUposMediator>();

        var inventory = Inventory.Create();
        var hardwareStatusManager = HardwareStatusManager.Create();

        // Mocking classes with dependencies, providing real objects for required parameters
        depositControllerMock = new Mock<DepositController>(
            inventory,
            hardwareStatusManager,
            managerMock?.Object ?? new Mock<CashChangerManager>(inventory, new Mock<TransactionHistory>().Object, (object?)null, new ConfigurationProvider()).Object,
            new ConfigurationProvider(),
            (TimeProvider?)null);
        inventoryMock = new Mock<Inventory>();
        managerMock = new Mock<CashChangerManager>(
            inventory,
            new Mock<TransactionHistory>().Object,
            (object?)null,
            new ConfigurationProvider());
        diagnosticControllerMock = new Mock<DiagnosticController>(inventory, hardwareStatusManager);
    }

    /// <summary>DepositFacade.BeginDeposit が対応するプロトコルコマンドを実行することを検証します。</summary>
    [Fact]
    public void DepositFacadeBeginDepositShouldExecuteCommand()
    {
        // Arrange
        var facade = new DepositFacade(depositControllerMock.Object, mediatorMock.Object);

        // Act
        facade.BeginDeposit();

        // Assert
        mediatorMock.Verify(m => m.Execute(It.IsAny<BeginDepositCommand>()), Times.Once);
    }

    /// <summary>InventoryFacade.ReadCashCounts が対応するプロトコルコマンドを実行することを検証します。</summary>
    [Fact]
    public void InventoryFacadeReadCashCountsShouldExecuteCommand()
    {
        // Arrange
        var facade = new InventoryFacade(inventoryMock.Object, managerMock.Object, mediatorMock.Object);

        // Act
        facade.ReadCashCounts("JPY", 1.0m);

        // Assert
        mediatorMock.Verify(m => m.Execute(It.IsAny<ReadCashCountsCommand>()), Times.Once);
    }

    /// <summary>DiagnosticsFacade.CheckHealth が対応するプロトコルコマンドを実行することを検証します。</summary>
    [Fact]
    public void DiagnosticsFacadeCheckHealthShouldExecuteCommand()
    {
        // Arrange
        var facade = new DiagnosticsFacade(diagnosticControllerMock.Object, mediatorMock.Object);

        // Act
        facade.CheckHealth(HealthCheckLevel.Internal);

        // Assert
        mediatorMock.Verify(m => m.Execute(It.IsAny<CheckHealthCommand>()), Times.Once);
    }
}
