using CashChangerSimulator.Device.PosForDotNet.Services;
using Microsoft.PointOfService;
using Moq;
using Shouldly;

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

    /// <summary>DataEventEnabled が偽の場合に DataEvent がキューイングされないことを検証します。</summary>
    [Fact]
    public void QueueEventShouldNotCallQueueDataEventWhenDataEventDisabled()
    {
        var args = new DataEventArgs(100);
        mockSink.Setup(s => s.CapDepositDataEvent).Returns(true);
        mockSink.Setup(s => s.DataEventEnabled).Returns(false);

        notifier.QueueEvent(args);

        mockSink.Verify(s => s.QueueDataEvent(It.IsAny<DataEventArgs>()), Times.Never);
    }

    /// <summary>CapDepositDataEvent が偽の場合に DataEvent がキューイングされないことを検証します。</summary>
    [Fact]
    public void QueueEventShouldNotCallQueueDataEventWhenCapDisabled()
    {
        var args = new DataEventArgs(100);
        mockSink.Setup(s => s.CapDepositDataEvent).Returns(false);
        mockSink.Setup(s => s.DataEventEnabled).Returns(true);

        notifier.QueueEvent(args);

        mockSink.Verify(s => s.QueueDataEvent(It.IsAny<DataEventArgs>()), Times.Never);
    }

    /// <summary>NotifyEvent がシンクへ委譲されることを検証します。</summary>
    [Fact]
    public void NotifyEventShouldDelegateToSink()
    {
        var args = EventArgs.Empty;
        notifier.NotifyEvent(args);
        mockSink.Verify(s => s.NotifyEvent(args), Times.Once);
    }

    /// <summary>FireEvent がシンクへ委譲されることを検証します。</summary>
    [Fact]
    public void FireEventShouldDelegateToSink()
    {
        var args = EventArgs.Empty;
        notifier.FireEvent(args);
        mockSink.Verify(s => s.NotifyEvent(args), Times.Once); // FireEvent calls NotifyEvent
    }

    /// <summary>各プロパティがシンクへ委譲されることを検証します。</summary>
    [Fact]
    public void PropertiesShouldDelegateToSink()
    {
        mockSink.Setup(s => s.State).Returns(ControlState.Idle);
        notifier.State.ShouldBe(ControlState.Idle);

        mockSink.Setup(s => s.DeviceEnabled).Returns(true);
        notifier.DeviceEnabled.ShouldBe(true);
        notifier.DeviceEnabled = false;
        mockSink.VerifySet(s => s.DeviceEnabled = false);

        mockSink.Setup(s => s.Claimed).Returns(true);
        notifier.Claimed.ShouldBe(true);
        notifier.Claimed = false;
        mockSink.VerifySet(s => s.Claimed = false);

        mockSink.Setup(s => s.ClaimedByAnother).Returns(true);
        notifier.ClaimedByAnother.ShouldBe(true);
        notifier.ClaimedByAnother = false;
        mockSink.VerifySet(s => s.ClaimedByAnother = false);

        mockSink.Setup(s => s.RealTimeDataEnabled).Returns(true);
        notifier.RealTimeDataEnabled.ShouldBe(true);

        mockSink.Setup(s => s.DisableUposEventQueuing).Returns(true);
        notifier.DisableUposEventQueuing.ShouldBe(true);

        mockSink.Setup(s => s.AsyncResultCode).Returns(123);
        notifier.AsyncResultCode.ShouldBe(123);
        notifier.AsyncResultCode = 456;
        mockSink.VerifySet(s => s.AsyncResultCode = 456);

        mockSink.Setup(s => s.AsyncResultCodeExtended).Returns(789);
        notifier.AsyncResultCodeExtended.ShouldBe(789);
        notifier.AsyncResultCodeExtended = 101;
        mockSink.VerifySet(s => s.AsyncResultCodeExtended = 101);
    }

    /// <summary>未知の EventArgs 型が無視されることを検証します。</summary>
    [Fact]
    public void QueueEventShouldIgnoreUnknownEventArgs()
    {
        notifier.QueueEvent(EventArgs.Empty);
        // Verify no calls to any queue methods
        mockSink.Verify(s => s.QueueDataEvent(It.IsAny<DataEventArgs>()), Times.Never);
        mockSink.Verify(s => s.QueueStatusUpdateEvent(It.IsAny<StatusUpdateEventArgs>()), Times.Never);
        mockSink.Verify(s => s.QueueEvent(EventArgs.Empty), Times.Once);
    }

    /// <summary>Initialize メソッドでシンクを更新できることを検証します。</summary>
    [Fact]
    public void InitializeShouldUpdateSink()
    {
        var newMock = new Mock<IUposEventSink>();
        var defaultNotifier = new UposEventNotifier();
        defaultNotifier.Initialize(newMock.Object);
        
        newMock.Setup(s => s.State).Returns(ControlState.Busy);
        defaultNotifier.State.ShouldBe(ControlState.Busy);
    }
}
