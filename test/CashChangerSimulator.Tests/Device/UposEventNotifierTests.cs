using Microsoft.PointOfService;
using CashChangerSimulator.Device.Services;
using Moq;

namespace CashChangerSimulator.Tests.Device;

public class UposEventNotifierTests
{
    private readonly Mock<IUposEventSink> _mockSink;
    private readonly UposEventNotifier _notifier;

    public UposEventNotifierTests()
    {
        _mockSink = new Mock<IUposEventSink>();
        _notifier = new UposEventNotifier(_mockSink.Object);
    }

    [Fact]
    public void QueueEvent_ShouldCallQueueDataEvent_WhenEventArgsIsDataEventArgs()
    {
        // Arrange
        var args = new DataEventArgs(12345);
        _mockSink.Setup(s => s.CapDepositDataEvent).Returns(true);
        _mockSink.Setup(s => s.DataEventEnabled).Returns(true);

        // Act
        _notifier.QueueEvent(args);

        // Assert
        _mockSink.Verify(s => s.QueueDataEvent(args), Times.Once);
    }

    [Fact]
    public void QueueEvent_ShouldCallQueueStatusUpdateEvent_WhenEventArgsIsStatusUpdateEventArgs()
    {
        // Arrange
        var args = new StatusUpdateEventArgs(1);

        // Act
        _notifier.QueueEvent(args);

        // Assert
        _mockSink.Verify(s => s.QueueStatusUpdateEvent(args), Times.Once);
    }
}
