using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device.PosForDotNet;
using CashChangerSimulator.Device.PosForDotNet.Coordination;
using CashChangerSimulator.Device.Virtual;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.PointOfService;
using Moq;

namespace CashChangerSimulator.Tests.Device;

/// <summary>StatusCoordinator の動作を検証するテストクラス。</summary>
public class StatusCoordinatorTest
{
    private readonly Mock<ICashChangerStatusSink> mockSink;
    private readonly HardwareStatusManager hardwareStatusManager;
    private readonly OverallStatusAggregator statusAggregator;
    private readonly DepositController depositController;
    private readonly DispenseController dispenseController;

    /// <summary>Initializes a new instance of the <see cref="StatusCoordinatorTest"/> class.StatusCoordinatorTest の新しいインスタンスを初期化します。</summary>
    public StatusCoordinatorTest()
    {
        mockSink = new Mock<ICashChangerStatusSink>();
        hardwareStatusManager = HardwareStatusManager.Create();
        statusAggregator = OverallStatusAggregator.Create(new List<CashStatusMonitor>());

        var inventory = Inventory.Create();
        var history = new TransactionHistory();
        var manager = new CashChangerManager(inventory, history, null, null);

        depositController = new DepositController(inventory, hardwareStatusManager, manager);
        dispenseController = new DispenseController(manager, inventory, new ConfigurationProvider(), NullLoggerFactory.Instance, hardwareStatusManager, new Mock<IDeviceSimulator>().Object, null);
    }

    /// <summary>ジャム状態が変化した際に適切なステータス更新イベントが発生することを確認します。</summary>
    [Fact]
    public void JamStatusShouldFireStatusUpdateEvent()
    {
        // Arrange
        var coordinator = new StatusCoordinator(
            mockSink.Object,
            statusAggregator,
            hardwareStatusManager,
            depositController,
            dispenseController);
        coordinator.Start();

        // Act
        hardwareStatusManager.Input.IsJammed.Value = true;

        // Assert
        mockSink.Verify(s => s.FireEvent(It.Is<StatusUpdateEventArgs>(e => e.Status == (int)UposCashChangerStatusUpdateCode.Jam)), Times.Once);

        // Act - Reset Jam
        hardwareStatusManager.Input.IsJammed.Value = false;

        // Assert - Ok fires on reset
        mockSink.Verify(s => s.FireEvent(It.Is<StatusUpdateEventArgs>(e => e.Status == (int)UposCashChangerStatusUpdateCode.Ok)), Times.Once);
    }
}
