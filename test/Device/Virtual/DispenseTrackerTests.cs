using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device.Virtual;
using PosSharp.Abstractions;
using R3;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Tests.Device.Virtual;

public class DispenseTrackerTests : IDisposable
{
    private readonly DispenseTracker _target;

    public DispenseTrackerTests()
    {
        _target = new DispenseTracker();
    }

    public void Dispose()
    {
        _target.Dispose();
    }

    [Fact]
    public void CreateNewToken_ReturnsValidToken()
    {
        // Act
        var token = _target.CreateNewToken();

        // Assert
        token.ShouldNotBe(default);
        token.IsCancellationRequested.ShouldBeFalse();
    }

    [Fact]
    public void CreateNewToken_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        _target.Dispose();

        // Act & Assert
        Should.Throw<ObjectDisposedException>(() => _target.CreateNewToken());
    }

    [Fact]
    public void CancelCurrent_WhenNoToken_ReturnsFalse()
    {
        // Act
        var result = _target.CancelCurrent();

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void CancelCurrent_WhenTokenExists_CancelsAndReturnsTrue()
    {
        // Arrange
        var token = _target.CreateNewToken();

        // Act
        var result = _target.CancelCurrent();

        // Assert
        result.ShouldBeTrue();
        token.IsCancellationRequested.ShouldBeTrue();
    }

    [Fact]
    public void ResetToken_DisposesCurrentToken()
    {
        // Arrange
        var token = _target.CreateNewToken();

        // Act
        _target.ResetToken();

        // Assert
        // We can't directly check if it's disposed easily without internal access,
        // but we can check if a new token can be created (to ensure no state corruption).
        var newToken = _target.CreateNewToken();
        newToken.ShouldNotBe(token);
    }

    [Fact]
    public void NotifyMethods_FireEvents()
    {
        // Arrange
        var changedFired = false;
        var completeFired = false;
        var errorFired = false;

        using var sub1 = _target.Changed.Subscribe(_ => changedFired = true);
        using var sub2 = _target.OutputCompleteEvents.Subscribe(_ => completeFired = true);
        using var sub3 = _target.ErrorEvents.Subscribe(_ => errorFired = true);

        // Act
        _target.NotifyChanged();
        _target.NotifyComplete();
        _target.NotifyError(DeviceErrorCode.Failure, 0);

        // Assert
        changedFired.ShouldBeTrue();
        completeFired.ShouldBeTrue();
        errorFired.ShouldBeTrue();
    }

    [Theory]
    [InlineData(typeof(DeviceException), DeviceErrorCode.Illegal, 123)]
    [InlineData(typeof(Exception), DeviceErrorCode.Failure, 0)]
    public void HandleDispenseError_MapsCorrectly(Type exceptionType, DeviceErrorCode expectedCode, int expectedEx)
    {
        // Arrange
        Exception ex;
        if (exceptionType == typeof(DeviceException))
        {
            ex = new DeviceException("test", DeviceErrorCode.Illegal, 123);
        }
        else
        {
            ex = new Exception("test");
        }

        // Act
        DispenseTracker.HandleDispenseError(ex, out var code, out var codeEx);

        // Assert
        code.ShouldBe(expectedCode);
        codeEx.ShouldBe(expectedEx);
    }
}
