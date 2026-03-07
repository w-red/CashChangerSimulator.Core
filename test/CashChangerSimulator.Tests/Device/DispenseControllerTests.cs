using CashChangerSimulator.Core.Configuration;
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
using Xunit;

namespace CashChangerSimulator.Tests.Device;

public class DispenseControllerTests
{
    private readonly Inventory _inventory;
    private readonly HardwareStatusManager _hw;
    private readonly Mock<CashChangerManager> _mockManager;
    private readonly Mock<IDeviceSimulator> _mockSimulator;
    private readonly DispenseController _controller;

    public DispenseControllerTests()
    {
        _inventory = new Inventory();
        _hw = new HardwareStatusManager();
        _mockManager = new Mock<CashChangerManager>(_inventory, new TransactionHistory(), new ChangeCalculator());
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
    public void ClearError_ShouldResetStatusFromErrorToIdle()
    {
        // Arrange
        // (Simulate an error state by triggering an exception in a test-like manner or reflection)
        // For simplicity, let's use the actual failure path
        _mockManager.Setup(m => m.Dispense(It.IsAny<decimal>(), It.IsAny<string>()))
            .Throws(new Exception("Fail"));

        Should.ThrowAsync<Exception>(() => _controller.DispenseChangeAsync(100, false, (e, ex) => { }));
        _controller.Status.ShouldBe(CashDispenseStatus.Error);

        // Act
        _controller.ClearError();

        // Assert
        _controller.Status.ShouldBe(CashDispenseStatus.Idle);
    }
}
