using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Device.PosForDotNet;
using CashChangerSimulator.Device.Virtual;
using Microsoft.PointOfService;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>非同期モードにおけるデバイス動作の信頼性と状態の整合性を検証するテストクラス。</summary>
/// <remarks>
/// 非同期払い出し操作において、完了イベントが通知された瞬間の内部状態（Status, ResultCode）が
/// 規約通りであることをタイムクリティカルな条件下で検証します。
/// </remarks>
public class AsyncModeReliabilityTests
{
    private class ReliabilityTestChanger(Inventory inv, CashChangerManager manager, DispenseController controller, HardwareStatusManager hw) : InternalSimulatorCashChanger(
        inventory: inv, manager: manager, dispenseController: controller, hardwareStatusManager: hw)
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
                eventHistory.Add(e);
                if (e is StatusUpdateEventArgs se && se.Status == 91)
                {
                    // [IMPORTANT] Capture internal state AT THE MOMENT of event notification.
                    // Access through the mediator's context to avoid any instance mismatch.
                    StatusAtEvent = DispenseController.Status;
                    ResultCodeAtEvent = (int)AsyncResultCode;
                    CompletionSignal.Set();
                }
            }
        }

        public void SetBackgroundException(Exception ex) => BackgroundException = ex;

        public bool WaitForEvent(int timeoutMs) => CompletionSignal.Wait(timeoutMs);
    }

    /// <summary>非同期出金処理において、完了イベント通知時の内部状態が正しいことを検証します。</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task AsyncDispenseShouldHaveCorrectStateWhenEventFires()
    {
        // Arrange
        var configProvider = new ConfigurationProvider();
        var config = new SimulatorConfiguration(); // Use default in-memory config
        configProvider.Update(config);

        var inventory = new Inventory();
        inventory.SetCount(new DenominationKey(100, CurrencyCashType.Coin, "JPY"), 10);

        var manager = new MockCashChangerManager(inventory, configProvider); // This mock uses base.Dispense
        var hardwareStatus = new HardwareStatusManager();

        var hardwareSim = new Mock<IDeviceSimulator>();
        hardwareSim.Setup(x => x.SimulateDispenseAsync(It.IsAny<CancellationToken>()))
                   .Returns(Task.CompletedTask);

        var controller = new DispenseController(manager, hardwareStatus, hardwareSim.Object);
        var changer = new ReliabilityTestChanger(inventory, manager, controller, hardwareStatus)
        {
            SkipStateVerification = true
        };

        // Act
        changer.Open();
        changer.Claim(1000);
        changer.DeviceEnabled = true;
        changer.AsyncMode = true;

        // Capture background exceptions properly for diagnostic output
        Func<Task> act = async () =>
        {
            try
            {
                changer.DispenseChange(100);
            }
            catch (Exception ex)
            {
                changer.SetBackgroundException(ex);
            }
        };
        await Task.Run(act, TestContext.Current.CancellationToken).ConfigureAwait(false);

        // Assert: Ensure it's busy immediately
        changer.DispenseController.IsBusy.ShouldBeTrue("Dispense operation should be busy.");

        // Allow the dispense to complete
        manager.DispenseFinishSignal.Set();

        // Wait for event capture with a generous timeout
        bool eventFired = changer.WaitForEvent(5000);

        // Assert consistency AT EVENT RECEIPT
        eventFired.ShouldBeTrue("Completion event was not fired within 5 seconds.");

        changer.BackgroundException.ShouldBeNull($"Background execution should not throw but threw: {changer.BackgroundException?.Message} (Type: {changer.BackgroundException?.GetType().Name})");

        changer.StatusAtEvent.ShouldBe(CashDispenseStatus.Idle, $"Internal status should be Idle when AsyncFinished event is fired, but was {changer.StatusAtEvent}.");
        changer.ResultCodeAtEvent.ShouldBe((int)ErrorCode.Success, $"AsyncResultCode should be Success (0) when event is fired, but was {changer.ResultCodeAtEvent} (Error? {(ErrorCode)changer.ResultCodeAtEvent}).\n" +
            $"Capture Details: StatusAtEvent={changer.StatusAtEvent}, ResultCodeAtEvent={changer.ResultCodeAtEvent}, IsBusyNow={changer.DispenseController.IsBusy}");
    }
}
