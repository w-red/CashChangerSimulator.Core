using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Opos;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Device.PosForDotNet;
using CashChangerSimulator.Device.Virtual;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.PointOfService;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>非同期モードにおけるデバイス動作�E信頼性と状態�E整合性を検証するチE��トクラス</summary>
/// <remarks>
/// 非同期払ぁE�Eし操作において、完亁E��ベントが通知された瞬間�E冁E��状慁EStatus, ResultCode)ぁE
/// 規紁E��りであることをタイムクリチE��カルな条件下で検証します、E
/// </remarks>
public class AsyncModeReliabilityTests
{
    private class ReliabilityTestChanger(Inventory inv, CashChangerManager manager, DispenseController controller, HardwareStatusManager hw) : InternalSimulatorCashChanger(
        configProvider: null, inventory: inv, manager: manager, dispenseController: controller, hardwareStatusManager: hw)
    {
        public CashDispenseStatus StatusAtEvent { get; private set; }
        public int ResultCodeAtEvent { get; private set; }
        public ManualResetEventSlim CompletionSignal { get; } = new(false);
        public Exception? BackgroundException { get; private set; }

        private readonly List<EventArgs> eventHistory = [];
        private readonly Lock @lock = new();

        protected override void NotifyEvent(EventArgs e)
        {
            lock (@lock)
            {
                if (e is StatusUpdateEventArgs se && se.Status == (int)UposCashChangerStatusUpdateCode.AsyncFinished)
                {
                    // [IMPORTANT] Capture internal state AT THE MOMENT of event notification.
                    // Access through the mediator's context to avoid any instance mismatch.
                    StatusAtEvent = DispenseController.Status;
                    ResultCodeAtEvent = AsyncResultCode;
                    CompletionSignal.Set();
                }
            }
        }

        public void SetBackgroundException(Exception ex) => BackgroundException = ex;

        public bool WaitForEvent(int timeoutMs) => CompletionSignal.Wait(timeoutMs);
    }

    /// <summary>非同期�E金�E琁E��おいて、完亁E��ベント通知時�E冁E��状態が正しいことを検証します</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task AsyncDispenseShouldHaveCorrectStateWhenEventFires()
    {
        // Arrange
        var configProvider = new ConfigurationProvider();
        var config = new SimulatorConfiguration(); // Use default in-memory config
        configProvider.Update(config);

        var inventory = Inventory.Create();
        inventory.SetCount(new DenominationKey(100, CurrencyCashType.Coin, "JPY"), 10);

        var manager = new MockCashChangerManager(inventory, configProvider); // This mock uses base.Dispense
        var hardwareStatus = HardwareStatusManager.Create();

        var hardwareSim = new Mock<IDeviceSimulator>();
        var dispenseTcs = new TaskCompletionSource();
        hardwareSim.Setup(x => x.SimulateDispenseAsync(It.IsAny<CancellationToken>()))
                   .Returns(dispenseTcs.Task);

        var timeProvider = new FakeTimeProvider();
        var controller = new DispenseController(manager, inventory, configProvider, NullLoggerFactory.Instance, hardwareStatus, hardwareSim.Object);
        var changer = new ReliabilityTestChanger(inventory, manager, controller, hardwareStatus)
        {
            SkipStateVerification = false
        };

        // Act
        changer.Open();
        changer.Claim(1000);
        changer.DeviceEnabled = true;
        changer.AsyncMode = true;

        // Start dispense
        changer.DispenseChange(100);

        // Assert: Ensure it's busy immediately
        changer.DispenseController.IsBusy.ShouldBeTrue("Dispense operation should be busy.");

        // Allow the dispense to complete
        dispenseTcs.SetResult();
        manager.DispenseFinishSignal.Set();

        // Advance virtual time to complete the background task
        timeProvider.Advance(TimeSpan.FromMilliseconds(20));

        // Wait for event capture with a short timeout as it should be immediate after Advance
        bool eventFired = changer.WaitForEvent(1000);

        // Assert consistency AT EVENT RECEIPT
        eventFired.ShouldBeTrue("Completion event was not fired within 5 seconds.");

        changer.BackgroundException.ShouldBeNull($"Background execution should not throw but threw: {changer.BackgroundException?.Message} (Type: {changer.BackgroundException?.GetType().Name})");

        changer.StatusAtEvent.ShouldBe(CashDispenseStatus.Idle, $"Internal status should be Idle when AsyncFinished event is fired, but was {changer.StatusAtEvent}.");
        changer.ResultCodeAtEvent.ShouldBe((int)ErrorCode.Success, $"AsyncResultCode should be Success (0) when event is fired, but was {changer.ResultCodeAtEvent} (Error? {(ErrorCode)changer.ResultCodeAtEvent}).\n" +
            $"Capture Details: StatusAtEvent={changer.StatusAtEvent}, ResultCodeAtEvent={changer.ResultCodeAtEvent}, IsBusyNow={changer.DispenseController.IsBusy}");
    }
}

