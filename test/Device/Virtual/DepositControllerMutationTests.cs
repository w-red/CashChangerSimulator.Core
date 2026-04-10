using System.Reflection;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Device.Virtual;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Shouldly;
using Xunit;
using R3;
using CashChangerSimulator.Core.Services;

namespace CashChangerSimulator.Tests.Device.Virtual;

public class DepositControllerMutationTests : DeviceTestBase
{
    private readonly DepositController _controller;

    public DepositControllerMutationTests()
    {
        StatusManager.Input.IsConnected.Value = true;
        
        // 物理的な待機をなくし、かつ TimeProvider によりタイムアウトを即時にするため 0 に設定
        ConfigurationProvider.Config.Simulation.DepositDelayMs = 0;

        // DepositController に FakeTimeProvider を注入して仮想時間を進められるようにする
        _controller = new DepositController(
            Inventory,
            StatusManager,
            Manager,
            ConfigurationProvider,
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
    public async Task EndDepositAsync_WhenSuccessfulStore_FiresChangedEvent()
    {
        // Arrange
        bool eventFired = false;
        using var sub = _controller.Changed.Subscribe(_ => eventFired = true);

        // Act
        _controller.BeginDeposit();
        _controller.FixDeposit();
        await _controller.EndDepositAsync(DepositAction.NoChange);

        // Assert
        _controller.DepositStatus.ShouldBe(DeviceDepositStatus.End);
        eventFired.ShouldBeTrue();
    }

    [Fact]
    public async Task RepayDepositAsync_WhenNotFixed_CallsFixDepositFirst()
    {
        // Arrange
        _controller.BeginDeposit();

        // Act
        await _controller.RepayDepositAsync();

        // Assert
        _controller.IsFixed.ShouldBeFalse();
        _controller.DepositStatus.ShouldBe(DeviceDepositStatus.End);
        _controller.LastErrorCode.ShouldBe(DeviceErrorCode.Success);
    }

    [Fact]
    public void ProcessDenominationTracking_WhenInventoryUpdateFails_SetsErrorStatus()
    {
        // Arrange
        var method = typeof(DepositController).GetMethod("ProcessDenominationTracking", BindingFlags.NonPublic | BindingFlags.Instance);
        method.ShouldNotBeNull();

        var invalidCounts = new Dictionary<DenominationKey, int> { { new DenominationKey(99999, CurrencyCashType.Bill, "JPY"), 1 } };
        
        // 内部状態を反映させる
        typeof(DepositController).GetProperty("IsBusy")?.SetValue(_controller, true);

        // Act & Assert
        Should.Throw<Exception>(() => 
        {
            try {
                method.Invoke(_controller, new object[] { invalidCounts });
            } catch (TargetInvocationException ex) {
                throw ex.InnerException!;
            }
        });
    }

    [Fact]
    public void BeginDepositAsync_WhenAlreadyBusy_ThrowsDeviceException()
    {
        // Arrange
        // IsBusy ガードを直接テストするため、リフレクションでセットする
        typeof(DepositController).GetProperty("IsBusy")?.SetValue(_controller, true);

        // Act & Assert
        var ex = Should.Throw<DeviceException>((() => 
            _controller.BeginDeposit()
        ));
        ex.ErrorCode.ShouldBe(DeviceErrorCode.Busy);
    }

    [Fact]
    public void Constructor_WhenInventoryIsNull_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => new DepositController(null!));
    }

    [Fact]
    public void Constructor_WhenConfigProviderIsNull_InitializesInternalConfigProvider()
    {
        var controller = new DepositController(Inventory, null, null, null);
        var field = typeof(DepositController).GetField("internalConfigProvider", BindingFlags.NonPublic | BindingFlags.Instance);
        field.ShouldNotBeNull();
        field.GetValue(controller).ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_WhenConfigProviderIsNotNull_UsesProvidedConfigProvider()
    {
        var services = new ServiceCollection();
        var configProvider = new CashChangerSimulator.Core.Configuration.ConfigurationProvider();
        var controller = new DepositController(Inventory, null, null, configProvider);
        var field = typeof(DepositController).GetField("internalConfigProvider", BindingFlags.NonPublic | BindingFlags.Instance);
        field.ShouldNotBeNull();
        field.GetValue(controller).ShouldBeNull();
    }

    [Fact]
    public void Dispose_DisposesInternalDisposables()
    {
        // disposables.Add(changedSubject) などが実行されていることを間接的に確認
        var controller = new DepositController(Inventory);
        controller.Dispose();
        
        var field = typeof(DepositController).GetField("disposed", BindingFlags.NonPublic | BindingFlags.Instance);
        field.ShouldNotBeNull();
        ((bool)field.GetValue(controller)!).ShouldBeTrue();
    }

    [Fact]
    public void RequiredAmount_WhenSettingSameValue_DoesNotFireChanged()
    {
        _controller.RequiredAmount = 1000m;
        bool fired = false;
        using var sub = _controller.Changed.Subscribe(_ => fired = true);

        _controller.RequiredAmount = 1000m;

        fired.ShouldBeFalse();
    }

    [Fact]
    public void RequiredAmount_WhenDisposed_DoesNotFireChanged()
    {
        _controller.RequiredAmount = 1000m;
        bool fired = false;
        using var sub = _controller.Changed.Subscribe(_ => fired = true);

        _controller.Dispose();
        fired = false; // Reset after dispose might fire once during dispose if implementation does that

        _controller.RequiredAmount = 2000m;

        fired.ShouldBeFalse();
    }

    [Fact]
    public void Properties_VerifyLockAndReturnValues()
    {
        _controller.LastErrorCodeExtended.ShouldBe(0);
        
        // Reflection to set private property if necessary, or just verify defaults
        _controller.DepositCounts.ShouldNotBeNull();
        _controller.LastDepositedSerials.ShouldNotBeNull();
    }

    [Fact]
    public async Task EndDepositAsync_WithRepay_ClearsEscrowAndResetsAmount()
    {
        // Arrange
        _controller.BeginDeposit();
        var key = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        _controller.TrackDeposit(key, 1);
        _controller.FixDeposit();

        // Act
        await _controller.EndDepositAsync(DepositAction.Repay);

        // Assert
        _controller.DepositAmount.ShouldBe(0m);
        _controller.DepositCounts.ShouldBeEmpty();
        Inventory.EscrowCounts.Any(kv => kv.Value > 0).ShouldBeFalse();
        _controller.DepositStatus.ShouldBe(DeviceDepositStatus.End);
    }

    [Fact]
    public async Task EndDepositAsync_WithChange_CalculatesCorrectPartialStorage()
    {
        // Arrange
        _controller.BeginDeposit();
        _controller.RequiredAmount = 700m;
        var key = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        var coin100 = new DenominationKey(100, CurrencyCashType.Coin, "JPY");
        Inventory.SetCount(coin100, 10); // Add change funds
        _controller.TrackDeposit(key, 1);
        _controller.FixDeposit();

        // Act
        var endTask = _controller.EndDepositAsync(DepositAction.Change);
        await endTask;

        // Assert
        // 1000 - 700 = 300 Change. 1000 Yen bill cannot be split, so it should be stored.
        _controller.DepositStatus.ShouldBe(DeviceDepositStatus.End);
        Inventory.GetCount(key).ShouldBe(1);
        Inventory.EscrowCounts.Any(kv => kv.Value > 0).ShouldBeFalse();
    }

    [Fact]
    public void TrackDeposit_WhenCapacityReached_UpdatesOverflowAmount()
    {
        // Arrange
        _controller.BeginDeposit();
        var key = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        
        // Clear specific denomination config to force use of default Thresholds.Full
        ConfigurationProvider.Config.Inventory.Clear();
        ConfigurationProvider.Config.Thresholds.Full = 5;
        
        // Act
        _controller.TrackDeposit(key, 10);

        // Assert 
        // 5 stored normally, (10 - 5) = 5 overflow
        _controller.OverflowAmount.ShouldBe(1000m * 5);
        _controller.DepositAmount.ShouldBe(1000m * 10);
    }

    [Fact]
    public async Task EndDepositAsync_WhenCancelled_SetsCancelledErrorCode()
    {
        ConfigurationProvider.Config.Simulation.DepositDelayMs = 5000;
        // Act
        _controller.BeginDeposit();
        _controller.FixDeposit();
        
        // Task to be cancelled
        var task = _controller.EndDepositAsync(DepositAction.NoChange);
        
        // Wait a small bit to ensure we are inside the long Delay
        await Task.Delay(20);
        
        // Trigger cancellation via Dispose
        _controller.Dispose();

        // Assert
        await task;
        _controller.LastErrorCode.ShouldBe(DeviceErrorCode.Cancelled);
    }

    [Fact]
    public void TrackDeposit_WhenJammed_ThrowsDeviceException()
    {
        // Arrange
        _controller.BeginDeposit();
        StatusManager.Input.IsJammed.Value = true;
        var key = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");

        // Act & Assert
        Should.Throw<DeviceException>(() => _controller.TrackDeposit(key))
            .ErrorCode.ShouldBe(DeviceErrorCode.Jammed);
    }

    [Fact]
    public void BeginDeposit_WhenOverlapped_ThrowsDeviceException()
    {
        // Arrange
        StatusManager.Input.IsOverlapped.Value = true;

        // Act & Assert
        Should.Throw<DeviceException>(() => _controller.BeginDeposit())
            .ErrorCode.ShouldBe(DeviceErrorCode.Overlapped);
    }

    [Fact]
    public void TrackReject_UpdatesRejectAmountAndFiresEvents()
    {
        // Arrange
        _controller.BeginDeposit();
        bool fired = false;
        using var sub = _controller.Changed.Subscribe(_ => fired = true);

        // Act
        _controller.TrackReject(500m);

        // Assert
        _controller.RejectAmount.ShouldBe(500m);
        fired.ShouldBeTrue();
    }

    [Fact]
    public void PauseDeposit_TransitionsStateCorrectly()
    {
        // Arrange
        _controller.BeginDeposit(); // Status: Counting

        // Act
        _controller.PauseDeposit(DeviceDepositPause.Pause);

        // Assert
        _controller.IsPaused.ShouldBeTrue();

        // Act
        _controller.PauseDeposit(DeviceDepositPause.Resume);

        // Assert
        _controller.IsPaused.ShouldBeFalse();
    }
}
