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

/// <summary>Test class for providing AsyncModeReliabilityTests functionality.</summary>
public class AsyncModeReliabilityTests
{
    private class ReliabilityTestChanger : SimulatorCashChanger
    {
        public CashDispenseStatus StatusAtEvent { get; private set; }
        public int ResultCodeAtEvent { get; private set; }
        public bool EventCaptured { get; private set; }

        private readonly DispenseController _controller;

        public ReliabilityTestChanger(Inventory inv, CashChangerManager manager, DispenseController controller) 
            : base(
                new ConfigurationProvider(), 
                inv, 
                new TransactionHistory(), 
                manager, 
                new DepositController(inv), 
                controller, 
                new OverallStatusAggregatorProvider(new MonitorsProvider(inv, new ConfigurationProvider(), new CurrencyMetadataProvider(new ConfigurationProvider()))), 
                new HardwareStatusManager())
        {
            _controller = controller;
        }

        protected override void NotifyEvent(EventArgs e)
        {
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

    /// <summary>Tests the behavior of AsyncDispenseShouldHaveCorrectStateWhenEventFires to ensure proper functionality.</summary>
    [Fact]
    public async Task AsyncDispenseShouldHaveCorrectStateWhenEventFires()
    {
        // Arrange
        var inventory = new Inventory();
        inventory.SetCount(new DenominationKey(100, MoneyKind4Opos.Currencies.Interfaces.CashType.Coin), 10);
        var manager = new MockCashChangerManager(inventory);
        var controller = new DispenseController(manager, null, new Mock<IDeviceSimulator>().Object);
        var changer = new ReliabilityTestChanger(inventory, manager, controller)
        {
            AsyncMode = true,
            SkipStateVerification = true
        };

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