using System;
using CashChangerSimulator.Device.Services;
using Microsoft.PointOfService;
using Moq;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Tests.Device;

public class UposEventNotifierTests
{
    private readonly Mock<IUposEventSink> _sinkMock;
    private readonly UposEventNotifier _notifier;

    public UposEventNotifierTests()
    {
        _sinkMock = new Mock<IUposEventSink>();
        _notifier = new UposEventNotifier(_sinkMock.Object);
    }

    [Fact]
    public void NotifyEventShouldQueueEventWhenNotSkipping()
    {
        _sinkMock.Setup(s => s.SkipStateVerification).Returns(false);
        var e = new StatusUpdateEventArgs(1);

        _notifier.NotifyEvent(e);

        _sinkMock.Verify(s => s.QueueEvent(e), Times.Once);
    }

    [Fact]
    public void NotifyEventShouldSkipWhenSkipStateVerificationIsTrue()
    {
        _sinkMock.Setup(s => s.SkipStateVerification).Returns(true);
        var e = new StatusUpdateEventArgs(1);

        _notifier.NotifyEvent(e);

        _sinkMock.Verify(s => s.QueueEvent(It.IsAny<EventArgs>()), Times.Never);
    }

    [Fact]
    public void FireEventShouldCallNotifyEvent()
    {
        _sinkMock.Setup(s => s.SkipStateVerification).Returns(false);
        var e = new StatusUpdateEventArgs(1);

        _notifier.FireEvent(e);

        _sinkMock.Verify(s => s.QueueEvent(e), Times.Once);
    }
}
