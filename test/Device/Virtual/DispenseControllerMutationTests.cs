using System.Reflection;
using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device.Virtual;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using PosSharp.Abstractions;
using PosSharp.Core;
using R3;
using Shouldly;
using Xunit;

namespace CashChangerSimulator.Tests.Device.Virtual;

public class DispenseControllerMutationTests : IDisposable
{
    private readonly Inventory _inventory;
    private readonly ConfigurationProvider _configProvider;
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;
    private readonly HardwareStatusManager _hardwareStatusManager;
    private readonly Mock<IDeviceSimulator> _mockSimulator;
    private readonly Mock<CashChangerManager> _mockManager;
    private readonly FakeTimeProvider _timeProvider;
    private readonly DispenseController _target;

    public DispenseControllerMutationTests()
    {
        _inventory = Inventory.Create();
        _configProvider = new ConfigurationProvider();
        _mockLoggerFactory = new Mock<ILoggerFactory>();
        _mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);
        _hardwareStatusManager = HardwareStatusManager.Create();
        _mockSimulator = new Mock<IDeviceSimulator>();
        _mockManager = new Mock<CashChangerManager>(_inventory, new TransactionHistory(), null!);
        _timeProvider = new FakeTimeProvider();

        _target = new DispenseController(
            _mockManager.Object,
            _inventory,
            _configProvider,
            _mockLoggerFactory.Object,
            _hardwareStatusManager,
            _mockSimulator.Object);

        // デフォルトで接続済みにしておく
        _hardwareStatusManager.Input.IsConnected.Value = true;
    }

    public void Dispose()
    {
        _target.Dispose();
        _hardwareStatusManager.Dispose();
    }

    private void SetControllerState(CashDispenseStatus status, DeviceErrorCode errorCode = DeviceErrorCode.Success)
    {
        var field = typeof(DispenseController).GetField("atomicState", BindingFlags.NonPublic | BindingFlags.Instance);
        var atomicState = field?.GetValue(_target);
        var transitionMethod = atomicState?.GetType().GetMethod("Transition");
        if (transitionMethod != null)
        {
            var newState = new DispenseState(status, errorCode, 0);
            var stateType = typeof(DispenseState);
            var funcType = typeof(Func<,>).MakeGenericType(stateType, stateType);
            var lambda = (Delegate)typeof(DispenseControllerMutationTests)
                .GetMethod(nameof(CreateSpecificState), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(stateType)
                .Invoke(null, [newState])!;

            transitionMethod.Invoke(atomicState, [lambda]);
        }
    }

    private static Func<T, T> CreateSpecificState<T>(T state) => _ => state;

    [Fact]
    public async Task DispenseChangeAsync_Success_CallsManagerAndSimulator()
    {
        // Arrange
        var key = new DenominationKey(1000, CurrencyCashType.Bill);
        _inventory.SetCount(key, 10);
        _mockSimulator.Setup(s => s.SimulateDispenseAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        await _target.DispenseChangeAsync(1000, false);

        // Assert
        _mockSimulator.Verify(s => s.SimulateDispenseAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockManager.Verify(m => m.Dispense(It.Is<IReadOnlyDictionary<DenominationKey, int>>(d => d[key] == 1)), Times.Once);
        _target.Status.ShouldBe(CashDispenseStatus.Idle);
    }

    [Fact]
    public async Task DispenseChangeAsync_MultipleDenominations_CallsManagerWithCorrectCounts()
    {
        // Arrange
        var key1000 = new DenominationKey(1000, CurrencyCashType.Bill);
        var key5000 = new DenominationKey(5000, CurrencyCashType.Bill);
        _inventory.SetCount(key1000, 10);
        _inventory.SetCount(key5000, 10);
        _mockSimulator.Setup(s => s.SimulateDispenseAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        // 7000 -> 1x5000 + 2x1000
        await _target.DispenseChangeAsync(7000, false);

        // Assert
        _mockManager.Verify(m => m.Dispense(It.Is<IReadOnlyDictionary<DenominationKey, int>>(d => 
            d[key5000] == 1 && d[key1000] == 2)), Times.Once);
    }

    [Fact]
    public async Task DispenseChangeAsync_WhenDisconnected_ThrowsDeviceException()
    {
        // Arrange
        _hardwareStatusManager.Input.IsConnected.Value = false;

        // Act & Assert
        await Should.ThrowAsync<DeviceException>(() => _target.DispenseChangeAsync(1000, false));
    }

    [Fact]
    public async Task DispenseCashAsync_Success_FiresOutputCompleteEvent()
    {
        // Arrange
        var eventFired = false;
        using var sub = _target.OutputCompleteEvents.Subscribe(_ => eventFired = true);
        _mockSimulator.Setup(s => s.SimulateDispenseAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        await _target.DispenseCashAsync(new Dictionary<DenominationKey, int>(), false);

        // Assert
        eventFired.ShouldBeTrue();
    }

    [Fact]
    public async Task DispenseCashAsync_Cancellation_HandlesGracefully()
    {
        // Arrange
        var tcs = new TaskCompletionSource();
        var changedCount = 0;
        using var sub = _target.Changed.Subscribe(_ => changedCount++);

        _mockSimulator.Setup(s => s.SimulateDispenseAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(async t => 
            {
                using (t.Register(() => tcs.TrySetResult()))
                {
                    await tcs.Task;
                    throw new OperationCanceledException(t);
                }
            });

        var dispenseTask = _target.DispenseCashAsync(new Dictionary<DenominationKey, int>(), false);

        // Act
        _target.ClearOutput();
        await dispenseTask;

        // Assert
        _target.Status.ShouldBe(CashDispenseStatus.Idle);
        _target.LastErrorCode.ShouldBe(DeviceErrorCode.Cancelled);
        
        // Verify Changed event was fired during cancellation
        changedCount.ShouldBeGreaterThanOrEqualTo(2); // Prepare + Cancellation
    }

    [Fact]
    public async Task DispenseCashAsync_HardwareError_SetsErrorStatus()
    {
        // Arrange
        _mockSimulator.Setup(s => s.SimulateDispenseAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DeviceException("Hardware failure", DeviceErrorCode.Failure));

        // Act
        var changedFired = false;
        var errorFired = false;
        using var subChanged = _target.Changed.Subscribe(_ => changedFired = true);
        using var subError = _target.ErrorEvents.Subscribe(_ => errorFired = true);

        await _target.DispenseCashAsync(new Dictionary<DenominationKey, int>(), false);

        // Assert
        _target.Status.ShouldBe(CashDispenseStatus.Error);
        _target.LastErrorCode.ShouldBe(DeviceErrorCode.Failure);
        changedFired.ShouldBeTrue();
        errorFired.ShouldBeTrue();
    }

    [Fact]
    public async Task DispenseCashAsync_WhenJammed_ThrowsDeviceException()
    {
        // Arrange
        _hardwareStatusManager.Input.IsJammed.Value = true;

        // Act & Assert
        await Should.ThrowAsync<DeviceException>(() => _target.DispenseCashAsync(new Dictionary<DenominationKey, int>(), false));
    }

    [Fact]
    public async Task DispenseCashAsync_WhenBusy_ThrowsDeviceException()
    {
        // Arrange
        var tcs = new TaskCompletionSource();
        _mockSimulator.Setup(s => s.SimulateDispenseAsync(It.IsAny<CancellationToken>())).Returns(tcs.Task);

        var firstTask = _target.DispenseCashAsync(new Dictionary<DenominationKey, int>(), false);

        // Act & Assert
        await Should.ThrowAsync<DeviceException>(() => _target.DispenseCashAsync(new Dictionary<DenominationKey, int>(), false));

        tcs.SetResult();
        await firstTask;
    }

    [Fact]
    public void Methods_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        _target.Dispose();

        // Act & Assert
        Should.Throw<ObjectDisposedException>(() => _target.ClearOutput());
        Should.ThrowAsync<ObjectDisposedException>(() => _target.DispenseCashAsync(new Dictionary<DenominationKey, int>(), false)).Wait();
    }

    [Fact]
    public void NotifyChanged_CalledDuringTransitions()
    {
        // Arrange
        var changedCount = 0;
        using var sub = _target.Changed.Subscribe(_ => changedCount++);

        // Act
        // 1. PrepareDispense (Busy) -> +1
        // 2. FinalizeDispense (via successful task) -> +1
        var tcs = new TaskCompletionSource();
        _mockSimulator.Setup(s => s.SimulateDispenseAsync(It.IsAny<CancellationToken>())).Returns(tcs.Task);
        var task = _target.DispenseCashAsync(new Dictionary<DenominationKey, int>(), false);
        
        tcs.SetResult();
        task.Wait();

        // Assert
        changedCount.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Dispose_CancelsCurrentOperation()
    {
        // Arrange
        var tcs = new TaskCompletionSource();
        var canceled = false;
        _mockSimulator.Setup(s => s.SimulateDispenseAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(async t => 
            {
                using (t.Register(() => canceled = true))
                {
                    await tcs.Task;
                }
            });

        var task = _target.DispenseCashAsync(new Dictionary<DenominationKey, int>(), false);

        // Act
        _target.Dispose();
        tcs.TrySetResult();
        await task;

        // Assert
        canceled.ShouldBeTrue();
    }

    [Fact]
    public void HandleDispenseException_DirectCall_NotifiesChanged()
    {
        // Arrange
        var changedFired = false;
        using var sub = _target.Changed.Subscribe(_ => changedFired = true);

        // Act
        var method = typeof(DispenseController).GetMethod("HandleDispenseException", BindingFlags.NonPublic | BindingFlags.Instance);
        method!.Invoke(_target, [new Exception("Test error")]);

        // Assert
        changedFired.ShouldBeTrue();
        _target.Status.ShouldBe(CashDispenseStatus.Error);
    }

    [Fact]
    public void HandleDispenseCancellation_DirectCall_NotifiesObservers()
    {
        // Arrange
        var changedFired = false;
        using var sub = _target.Changed.Subscribe(_ => changedFired = true);
        var errorFired = false;
        using var errSub = _target.ErrorEvents.Subscribe(_ => errorFired = true);

        // Act
        var method = typeof(DispenseController).GetMethod("HandleDispenseCancellation", BindingFlags.NonPublic | BindingFlags.Instance);
        method!.Invoke(_target, null);

        // Assert
        changedFired.ShouldBeTrue();
        errorFired.ShouldBeTrue();
        _target.Status.ShouldBe(CashDispenseStatus.Idle);
    }

    [Fact]
    public async Task DispenseCashAsync_HardwareDisconnected_ThrowsException()
    {
        // Arrange
        _hardwareStatusManager.Input.IsConnected.Value = false;
        bool changedFired = false;
        using var d = _target.Changed.Subscribe(_ => changedFired = true);

        // Act & Assert
        var ex = await Should.ThrowAsync<DeviceException>(async () => 
            await _target.DispenseCashAsync(new Dictionary<DenominationKey, int>(), false));
        
        ex.ErrorCode.ShouldBe(DeviceErrorCode.Closed);
        _target.Status.ShouldBe(CashDispenseStatus.Idle);
        changedFired.ShouldBeFalse();
    }

    [Fact]
    public async Task DispenseCashAsync_HardwareJammed_ThrowsException()
    {
        // Arrange
        _hardwareStatusManager.Input.IsConnected.Value = true;
        _hardwareStatusManager.Input.IsJammed.Value = true;
        bool changedFired = false;
        using var d = _target.Changed.Subscribe(_ => changedFired = true);

        // Act & Assert
        var ex = await Should.ThrowAsync<DeviceException>(async () => 
            await _target.DispenseCashAsync(new Dictionary<DenominationKey, int>(), false));
        
        ex.ErrorCode.ShouldBe(DeviceErrorCode.Jammed);
        _target.Status.ShouldBe(CashDispenseStatus.Idle);
        changedFired.ShouldBeFalse();
    }

    [Fact]
    public async Task DispenseCashAsync_Cancellation_ResetsToIdle()
    {
        // Arrange
        var tcs = new TaskCompletionSource();
        _mockSimulator.Setup(s => s.SimulateDispenseAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken t) => {
                using (t.Register(() => tcs.TrySetResult()))
                {
                    await tcs.Task;
                }
                throw new OperationCanceledException(t);
            });

        var task = _target.DispenseCashAsync(new Dictionary<DenominationKey, int>(), false);
        
        // Act
        _target.ClearOutput();
        await task;
        
        // Assert
        _target.Status.ShouldBe(CashDispenseStatus.Idle);
        _target.LastErrorCode.ShouldBe(DeviceErrorCode.Cancelled);
    }

    [Fact]
    public void VerifySetControllerStateHelperWorks()
    {
        // Arrange
        SetControllerState(CashDispenseStatus.Busy, DeviceErrorCode.Failure);

        // Assert
        _target.Status.ShouldBe(CashDispenseStatus.Busy);
        _target.LastErrorCode.ShouldBe(DeviceErrorCode.Failure);

        // Reset
        SetControllerState(CashDispenseStatus.Idle, DeviceErrorCode.Success);
        _target.Status.ShouldBe(CashDispenseStatus.Idle);
    }

    [Fact]
    public void ClearOutput_WhenBusy_TransitionsToIdleAndFiresNotification()
    {
        // Arrange
        SetControllerState(CashDispenseStatus.Busy);
        int changedCount = 0;
        using var d = _target.Changed.Subscribe(_ => changedCount++);
        
        // Act
        _target.ClearOutput();

        // Assert
        _target.Status.ShouldBe(CashDispenseStatus.Idle);
        _target.LastErrorCode.ShouldBe(DeviceErrorCode.Cancelled);
        changedCount.ShouldBe(1);
    }

    [Fact]
    public void ClearOutput_WhenError_ResetsToIdleAndFiresNotification()
    {
        // Arrange
        SetControllerState(CashDispenseStatus.Error, DeviceErrorCode.Failure);
        int changedCount = 0;
        using var d = _target.Changed.Subscribe(_ => changedCount++);
        
        // Act
        _target.ClearOutput();

        // Assert
        _target.Status.ShouldBe(CashDispenseStatus.Idle);
        _target.LastErrorCode.ShouldBe(DeviceErrorCode.Success);
        changedCount.ShouldBe(1);
    }

    [Fact]
    public void ClearOutput_WhenIdle_DoesNothing()
    {
        // Arrange
        SetControllerState(CashDispenseStatus.Idle);
        int changedCount = 0;
        using var d = _target.Changed.Subscribe(_ => changedCount++);

        // Act
        _target.ClearOutput();

        // Assert
        changedCount.ShouldBe(0);
    }
}
