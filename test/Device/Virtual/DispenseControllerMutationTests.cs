using System.Reflection;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Device.Virtual;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using Xunit;
using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Monitoring;
using R3;

namespace CashChangerSimulator.Tests.Device.Virtual;

public class DispenseControllerMutationTests : DeviceTestBase
{
    private readonly Mock<IDeviceSimulator> _simulatorMock;
    private readonly DispenseController _controller;

    public DispenseControllerMutationTests()
    {
        _simulatorMock = new Mock<IDeviceSimulator>();
        StatusManager.Input.IsConnected.Value = true;
        _controller = new DispenseController(
            Manager,
            Inventory,
            ConfigurationProvider,
            NullLoggerFactory.Instance,
            StatusManager,
            _simulatorMock.Object,
            TimeProvider);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _controller?.Dispose();
        }
        base.Dispose(disposing);
    }

    [Fact]
    public void HandleDispenseError_WithPosControlException_ExtractsErrorCodeUsingReflection()
    {
        // Arrange
        var posException = new MockPosControlException(99, 100);
        
        var method = typeof(DispenseController).GetMethod("HandleDispenseError", BindingFlags.NonPublic | BindingFlags.Static);
        method.ShouldNotBeNull();

        object[] parameters = new object[] { posException, DeviceErrorCode.Success, 0 };

        // Act
        method.Invoke(_controller, parameters);

        // Assert
        var code = (DeviceErrorCode)parameters[1];
        var codeEx = (int)parameters[2];

        code.ShouldBe((DeviceErrorCode)99);
        codeEx.ShouldBe(100);
    }

    [Fact]
    public async Task ExecuteDispense_WhenOperationCanceled_SetsCancelledStatusInFinalize()
    {
        // Arrange
        var counts = new Dictionary<DenominationKey, int>();
        _simulatorMock.Setup(x => x.SimulateDispenseAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        ConfigurationProvider.Config.System.CurrencyCode = "JPY";

        // Act
        await _controller.DispenseCashAsync(counts, false);

        // Assert
        // OperationCanceledException の場合、isError=false となりステータスは Idle に戻る(コードは Cancelled)
        _controller.Status.ShouldBe(CashDispenseStatus.Idle);
        _controller.LastErrorCode.ShouldBe(DeviceErrorCode.Cancelled);
    }

    [Fact]
    public async Task ExecuteDispense_WhenDeviceExceptionOccurs_MapsCorrectErrorCode()
    {
        // Arrange
        var counts = new Dictionary<DenominationKey, int>();
        var devEx = new DeviceException("failure", DeviceErrorCode.Failure, 123);
        _simulatorMock.Setup(x => x.SimulateDispenseAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(devEx);

        // Act
        await _controller.DispenseCashAsync(counts, false);

        // Assert
        _controller.Status.ShouldBe(CashDispenseStatus.Error);
        _controller.LastErrorCode.ShouldBe(DeviceErrorCode.Failure);
        _controller.LastErrorCodeExtended.ShouldBe(123);
    }

    [Fact]
    public void Constructor_WhenManagerIsNull_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => new DispenseController(null!, Inventory, ConfigurationProvider, NullLoggerFactory.Instance, StatusManager, _simulatorMock.Object));
    }

    [Fact]
    public async Task DispenseChangeAsync_WhenNotConnected_ThrowsDeviceException()
    {
        // Arrange
        var counts = new Dictionary<DenominationKey, int>();
        StatusManager.Input.IsConnected.Value = false;
        
        // Act & Assert
        var ex = await Should.ThrowAsync<DeviceException>(async () => 
            await _controller.DispenseCashAsync(counts, false)
        );
        ex.ErrorCode.ShouldBe(DeviceErrorCode.Closed);
    }

    [Fact]
    public async Task DispenseChangeAsync_WhenJammed_ThrowsDeviceException()
    {
        // Arrange
        var counts = new Dictionary<DenominationKey, int>();
        StatusManager.Input.IsJammed.Value = true;
        
        // Act & Assert
        var ex = await Should.ThrowAsync<DeviceException>(async () => 
            await _controller.DispenseCashAsync(counts, false)
        );
        ex.ErrorCode.ShouldBe(DeviceErrorCode.Jammed);
    }

    [Fact]
    public void ClearOutput_WhenBusy_CancelsAndSetsStatusToIdle()
    {
        // Reflection to set Status to Busy
        typeof(DispenseController).GetProperty("Status")?.SetValue(_controller, CashDispenseStatus.Busy);
        
        _controller.ClearOutput();

        _controller.Status.ShouldBe(CashDispenseStatus.Idle);
        _controller.LastErrorCode.ShouldBe(DeviceErrorCode.Cancelled);
    }

    [Fact]
    public void Dispose_DisposesInternalDisposables()
    {
        var controller = new DispenseController(Manager, Inventory, ConfigurationProvider, NullLoggerFactory.Instance, StatusManager, _simulatorMock.Object);
        controller.Dispose();
        
        var field = typeof(DispenseController).GetField("disposed", BindingFlags.NonPublic | BindingFlags.Instance);
        field.ShouldNotBeNull();
        ((bool)field.GetValue(controller)!).ShouldBeTrue();
    }

    private class MockPosControlException : Exception
    {
        public int ErrorCode { get; }
        public int ErrorCodeExtended { get; }

        public MockPosControlException(int code, int codeEx) : base("Mock POS Error")
        {
            ErrorCode = code;
            ErrorCodeExtended = codeEx;
        }
    }
}
