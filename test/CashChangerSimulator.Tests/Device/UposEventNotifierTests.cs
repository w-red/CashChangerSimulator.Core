using Microsoft.PointOfService;
using CashChangerSimulator.Device.Services;
using Moq;

namespace CashChangerSimulator.Tests.Device;

/// <summary>UPOS イベント通知（DataEvent, StatusUpdateEvent）のキューイング処理を検証するテストクラス。</summary>
public class UposEventNotifierTests
{
    private readonly Mock<IUposEventSink> _mockSink;
    private readonly UposEventNotifier _notifier;

    public UposEventNotifierTests()
    {
        _mockSink = new Mock<IUposEventSink>();
        _notifier = new UposEventNotifier(_mockSink.Object);
    }

    /// <summary>DataEventArgs がキューイングされた際に、適切にイベントシンクへ委譲されることを検証します。</summary>
    [Fact]
    public void QueueEventShouldCallQueueDataEventWhenEventArgsIsDataEventArgs()
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

    /// <summary>StatusUpdateEventArgs がキューイングされた際に、適切にイベントシンクへ委譲されることを検証します。</summary>
    [Fact]
    public void QueueEventShouldCallQueueStatusUpdateEventWhenEventArgsIsStatusUpdateEventArgs()
    {
        // Arrange
        var args = new StatusUpdateEventArgs(1);

        // Act
        _notifier.QueueEvent(args);

        // Assert
        _mockSink.Verify(s => s.QueueStatusUpdateEvent(args), Times.Once);
    }
}
