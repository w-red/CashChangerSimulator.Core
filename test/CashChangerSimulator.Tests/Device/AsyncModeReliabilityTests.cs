using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device;
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
        new ConfigurationProvider(),
        inv,
        new TransactionHistory(),
        manager,
        new DepositController(inv, hw),
        controller,
        new OverallStatusAggregatorProvider(new MonitorsProvider(inv, new ConfigurationProvider(), new CurrencyMetadataProvider(new ConfigurationProvider()))),
        hw)
    {
        public CashDispenseStatus StatusAtEvent { get; private set; }
        public int ResultCodeAtEvent { get; private set; }
        public bool EventCaptured { get; private set; }

        private readonly DispenseController _controller = controller;
        private readonly List<System.EventArgs> _eventHistory = [];
 
        protected override void NotifyEvent(System.EventArgs e)
        {
            _eventHistory.Add(e);
            base.NotifyEvent(e);
            if (e is StatusUpdateEventArgs se && se.Status == 91)
            {
                // Capture internal state AT THE MOMENT of event notification
                StatusAtEvent = _controller.Status;
                ResultCodeAtEvent = AsyncResultCode;
                EventCaptured = true;
            }
            // base.NotifyEvent(e); // Avoid framework event queueing
        }
    }

    /// <summary>非同期出金処理において、完了イベント通知時の内部状態が正しいことを検証します。</summary>
    [Fact]
    public async Task AsyncDispenseShouldHaveCorrectStateWhenEventFires()
    {
        // Arrange
        var inventory = new Inventory();
        inventory.SetCount(new DenominationKey(100, CurrencyCashType.Coin), 10);
        var manager = new MockCashChangerManager(inventory);
        var hardware = new HardwareStatusManager();
        var controller = new DispenseController(manager, hardware, new Mock<IDeviceSimulator>().Object);
        var changer = new ReliabilityTestChanger(inventory, manager, controller, hardware)
        {
            AsyncMode = true,
            // SkipStateVerification = true
        };
        changer.SkipStateVerification = true;

        // Act
        changer.Open();
        changer.Claim(1000);
        changer.DeviceEnabled = true;

        changer.DispenseChange(100);

        // Assert: Capture whatever the state is during operation
        controller.IsBusy.ShouldBeTrue("Dispense operation should be busy.");

        // Let it finish
        manager.DispenseFinishSignal.Set();

        // Wait for event capture
        int timeout = 0;
        while (!changer.EventCaptured && timeout < 50)
        {
            await Task.Delay(TestTimingConstants.EventPropagationDelayMs, TestContext.Current.CancellationToken);
            timeout++;
        }

        // Assert consistency AT EVENT RECEIPT
        changer.EventCaptured.ShouldBeTrue("Completion event was not fired.");
        changer.StatusAtEvent.ShouldBe(CashDispenseStatus.Idle, "Internal status should be Idle when AsyncFinished event is fired.");
        changer.ResultCodeAtEvent.ShouldBe((int)ErrorCode.Success, "AsyncResultCode should be Success when event is fired.");
    }
}
