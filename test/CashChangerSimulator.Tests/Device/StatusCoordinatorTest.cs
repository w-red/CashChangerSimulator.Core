using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Coordination;
using Microsoft.PointOfService;
using Moq;

namespace CashChangerSimulator.Tests.Device;

/// <summary>StatusCoordinator の動作を検証するテストクラス。</summary>
public class StatusCoordinatorTest
{
    private readonly Mock<ICashChangerStatusSink> _mockSink;
    private readonly HardwareStatusManager _hardwareStatusManager;
    private readonly OverallStatusAggregator _statusAggregator;
    private readonly DepositController _depositController;
    private readonly DispenseController _dispenseController;

    /// <summary>StatusCoordinatorTest の新しいインスタンスを初期化します。</summary>
    public StatusCoordinatorTest()
    {
        _mockSink = new Mock<ICashChangerStatusSink>();
        _hardwareStatusManager = new HardwareStatusManager();
        _statusAggregator = new OverallStatusAggregator(new List<CashStatusMonitor>());

        var inventory = new Inventory();
        var history = new TransactionHistory();
        var calculator = new ChangeCalculator();
        var manager = new CashChangerManager(inventory, history, calculator);

        _depositController = new DepositController(inventory);
        _dispenseController = new DispenseController(manager);
    }

    /// <summary>ジャム状態が変化した際に適切なステータス更新イベントが発生することを確認します。</summary>
    [Fact]
    public void JamStatusShouldFireStatusUpdateEvent()
    {
        // Arrange
        var coordinator = new StatusCoordinator(
            _mockSink.Object,
            _statusAggregator,
            _hardwareStatusManager,
            _depositController,
            _dispenseController);
        coordinator.Start();

        // Act
        _hardwareStatusManager.SetJammed(true);

        // Assert
        _mockSink.Verify(s => s.FireEvent(It.Is<StatusUpdateEventArgs>(e => e.Status == (int)UposCashChangerStatusUpdateCode.Jam)), Times.Once);

        // Act - Reset Jam
        _hardwareStatusManager.SetJammed(false);

        // Assert - Ok fires at initial subscription + on reset, so at least twice
        _mockSink.Verify(s => s.FireEvent(It.Is<StatusUpdateEventArgs>(e => e.Status == (int)UposCashChangerStatusUpdateCode.Ok)), Times.AtLeast(2));
    }
}
