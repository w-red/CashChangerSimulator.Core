using CashChangerSimulator.Device.PosForDotNet.Services;
using Microsoft.PointOfService;
using Moq;

namespace CashChangerSimulator.Tests.Device;

/// <summary>UPOS イベント通知（DataEvent, StatusUpdateEvent）のキューイング処理を検証するテストクラス。</summary>
public class UposEventNotifierTests
{
    private readonly Mock<IUposEventSink> mockSink;
    private readonly UposEventNotifier notifier;

    public UposEventNotifierTests()
    {
        mockSink = new Mock<IUposEventSink>();
        notifier = new UposEventNotifier(mockSink.Object);
    }

    /// <summary>DataEventArgs がキューイングされた際に、適切にイベントシンクへ委譲されることを検証します。</summary>
    [Fact]
    public void QueueEventShouldCallQueueDataEventWhenEventArgsIsDataEventArgs()
    {
        // Arrange
        var args = new DataEventArgs(12345);
        mockSink.Setup(s => s.CapDepositDataEvent).Returns(true);
        mockSink.Setup(s => s.DataEventEnabled).Returns(true);

        // Act
        notifier.QueueEvent(args);

        // Assert
        mockSink.Verify(s => s.QueueDataEvent(args), Times.Once);
    }

    /// <summary>StatusUpdateEventArgs がキューイングされた際に、適切にイベントシンクへ委譲されることを検証します。</summary>
    [Fact]
    public void QueueEventShouldCallQueueStatusUpdateEventWhenEventArgsIsStatusUpdateEventArgs()
    {
        // Arrange
        var args = new StatusUpdateEventArgs(1);

        // Act
        notifier.QueueEvent(args);

        // Assert
        mockSink.Verify(s => s.QueueStatusUpdateEvent(args), Times.Once);
    }
}
