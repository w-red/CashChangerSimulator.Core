using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
using Microsoft.PointOfService;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

public class DispenseControllerTests
{
    private readonly Inventory Inventory;
    private readonly HardwareStatusManager _hw;
    private readonly Mock<CashChangerManager> _mockManager;
    private readonly Mock<IDeviceSimulator> _mockSimulator;
    private readonly DispenseController _controller;

    public DispenseControllerTests()
    {
        Inventory = new Inventory();
        _hw = new HardwareStatusManager();
        _mockManager = new Mock<CashChangerManager>(Inventory, new TransactionHistory(), new ChangeCalculator());
        _mockSimulator = new Mock<IDeviceSimulator>();
        _controller = new DispenseController(_mockManager.Object, _hw, _mockSimulator.Object);
        
        // Default connected state
        _hw.SetConnected(true);
    }

    [Fact]
    public async Task DispenseChangeAsync_ShouldThrowClosed_WhenNotConnected()
    {
        // Arrange
        _hw.SetConnected(false);

        // Act & Assert
        var ex = await Should.ThrowAsync<PosControlException>(() => 
            _controller.DispenseChangeAsync(100, false, (e, ex) => { }));
        ex.ErrorCode.ShouldBe(ErrorCode.Closed);
    }

    [Fact]
    public async Task DispenseChangeAsync_ShouldThrowFailure_WhenJammed()
    {
        // Arrange
        _hw.SetJammed(true);

        // Act & Assert
        var ex = await Should.ThrowAsync<PosControlException>(() => 
            _controller.DispenseChangeAsync(100, false, (e, ex) => { }));
        ex.ErrorCode.ShouldBe(ErrorCode.Failure);
    }

    [Fact]
    public async Task DispenseChangeAsync_ShouldHandleInsufficientCash()
    {
        // Arrange
        _mockManager.Setup(m => m.Dispense(It.IsAny<decimal>(), It.IsAny<string>()))
            .Throws(new InsufficientCashException("Shortage"));

        ErrorCode capturedError = ErrorCode.Success;
        int capturedExtended = 0;

        // Act & Assert
        var ex = await Should.ThrowAsync<PosControlException>(() => 
            _controller.DispenseChangeAsync(100, false, (e, ext) => 
            {
                capturedError = e;
                capturedExtended = ext;
            }));

        ex.ErrorCode.ShouldBe(ErrorCode.Extended);
        ex.ErrorCodeExtended.ShouldBe((int)UposCashChangerErrorCodeExtended.OverDispense);
        
        capturedError.ShouldBe(ErrorCode.Extended);
        capturedExtended.ShouldBe((int)UposCashChangerErrorCodeExtended.OverDispense);
        _controller.Status.ShouldBe(CashDispenseStatus.Error);
    }

    [Fact]
    public async Task DispenseCashAsync_ShouldSucceed_WithValidCounts()
    {
        // Arrange
        var counts = new Dictionary<DenominationKey, int> 
        { 
            { new DenominationKey(100, CurrencyCashType.Coin), 1 } 
        };

        bool completed = false;
        
        // Act
        await _controller.DispenseCashAsync(counts, false, (e, ext) => 
        {
            e.ShouldBe(ErrorCode.Success);
            completed = true;
        });

        // Assert
        completed.ShouldBeTrue();
        _mockManager.Verify(m => m.Dispense(counts), Times.Once);
        _controller.Status.ShouldBe(CashDispenseStatus.Idle);
    }

    [Fact]
    public async Task DispenseChangeAsync_ShouldThrowBusy_WhenAlreadyProcessing()
    {
        // Arrange
        // We need a way to keep it busy. We can use a Task that waits.
        _mockSimulator.Setup(s => s.SimulateDispenseAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken t) => await Task.Delay(1000, t));

        _ = _controller.DispenseChangeAsync(100, true, (e, ex) => { });
        await Task.Delay(50, TestContext.Current.CancellationToken); // Give it a bit of time to start

        // Act & Assert
        _controller.IsBusy.ShouldBeTrue();
        await Should.ThrowAsync<PosControlException>(() => 
            _controller.DispenseChangeAsync(100, false, (e, ex) => { }));
    }

    [Fact]
    public async Task DispenseChangeAsync_ShouldThrowFailure_WhenOverlapped()
    {
        // Arrange
        _hw.SetOverlapped(true);

        // Act & Assert
        var ex = await Should.ThrowAsync<PosControlException>(() => 
            _controller.DispenseChangeAsync(100, false, (e, ex) => { }));
        ex.ErrorCode.ShouldBe(ErrorCode.Failure);
    }

    [Fact]
    public async Task ClearOutput_ShouldCancelActiveDispense()
    {
        // Arrange
        bool wasCanceled = false;
        var tcs = new TaskCompletionSource<bool>();

        _mockSimulator.Setup(s => s.SimulateDispenseAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken t) => 
            {
                tcs.SetResult(true); // Signal that we have entered the mock
                try { await Task.Delay(5000, t); }
                catch (OperationCanceledException) { wasCanceled = true; throw; }
            });

        _ = _controller.DispenseChangeAsync(100, true, (e, ex) => { });
        
        // Wait until we are inside the simulator method
        await tcs.Task;
        
        // Act
        _controller.ClearOutput();
        
        // Wait a bit for the task to react to cancellation
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Assert
        _controller.IsBusy.ShouldBeFalse();
        wasCanceled.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteDispense_ShouldHandleUnexpectedException()
    {
        // Arrange
        _mockManager.Setup(m => m.Dispense(It.IsAny<decimal>(), It.IsAny<string>()))
            .Throws(new InvalidOperationException("Unexpected"));

        ErrorCode capturedError = ErrorCode.Success;

        // Act & Assert
        await Should.ThrowAsync<PosControlException>(() => 
            _controller.DispenseChangeAsync(100, false, (e, ext) => capturedError = e));
        
        capturedError.ShouldBe(ErrorCode.Failure);
        _controller.Status.ShouldBe(CashDispenseStatus.Error);
    }

    [Fact]
    public void ClearError_ShouldResetStatusFromErrorToIdle()
    {
        // Arrange
        _mockManager.Setup(m => m.Dispense(It.IsAny<decimal>(), It.IsAny<string>()))
            .Throws(new Exception("Fail"));

        Should.ThrowAsync<PosControlException>(() => _controller.DispenseChangeAsync(100, false, (e, ex) => { }));
        _controller.Status.ShouldBe(CashDispenseStatus.Error);

        // Act
        _controller.ClearError();

        // Assert
        _controller.Status.ShouldBe(CashDispenseStatus.Idle);
    }

    [Fact]
    public async Task ExecuteDispense_ShouldHandlePosControlException()
    {
        // Arrange
        _mockManager.Setup(m => m.Dispense(It.IsAny<decimal>(), It.IsAny<string>()))
            .Throws(new PosControlException("Explicit error", ErrorCode.Illegal, 123));

        ErrorCode capturedError = ErrorCode.Success;
        int capturedExtended = 0;

        // Act & Assert
        var ex = await Should.ThrowAsync<PosControlException>(() => 
            _controller.DispenseChangeAsync(100, false, (e, ext) => 
            {
                capturedError = e;
                capturedExtended = ext;
            }));
        
        ex.ErrorCode.ShouldBe(ErrorCode.Illegal);
        ex.ErrorCodeExtended.ShouldBe(123);
        capturedError.ShouldBe(ErrorCode.Illegal);
        capturedExtended.ShouldBe(123);
        _controller.Status.ShouldBe(CashDispenseStatus.Error);
    }

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        var controller = new DispenseController(_mockManager.Object, _hw, _mockSimulator.Object);
        controller.Dispose();
        // Second dispose should also not throw
        controller.Dispose();
    }
}
