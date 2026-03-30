using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Core.Monitoring;
using CashChangerSimulator.Device;
using CashChangerSimulator.Device.Models;
using CashChangerSimulator.Device.Services.ScriptCommands;
using CashChangerSimulator.Device.Services;
using CashChangerSimulator.Device.Commands;
using CashChangerSimulator.Device.Coordination;
using Microsoft.PointOfService;
using Moq;
using Shouldly;
using R3;
using Microsoft.Extensions.Logging;

namespace CashChangerSimulator.Tests.Device;

/// <summary>CashChangerSimulator.Device のカバレッジを 100% にするための網羅的テストクラス。</summary>
public class ExhaustiveDeviceTests : IDisposable
{
    private readonly Inventory _inventory;
    private readonly CashChangerManager _manager;
    private readonly HardwareStatusManager _hardwareStatusManager;
    private readonly Mock<IDeviceSimulator> _simulatorMock;
    private readonly DispenseController _controller;

    public ExhaustiveDeviceTests()
    {
        _inventory = new Inventory();
        var history = new TransactionHistory();
        var calculator = new ChangeCalculator();
        _manager = new CashChangerManager(_inventory, history, calculator);
        _hardwareStatusManager = new HardwareStatusManager();
        _simulatorMock = new Mock<IDeviceSimulator>();
        _controller = new DispenseController(_manager, _hardwareStatusManager, _simulatorMock.Object);
        
        _hardwareStatusManager.SetConnected(true);
    }

    public void Dispose()
    {
        _controller.Dispose();
        _hardwareStatusManager.Dispose();
    }

    [Fact]
    public async Task DispenseController_Flows()
    {
        var onCompleteCalled = false;
        ErrorCode lastError = ErrorCode.Success;
        Action<ErrorCode, int> onComplete = (err, ext) => { onCompleteCalled = true; lastError = err; };

        // Normal path
        var key = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        _inventory.SetCount(key, 10);
        _simulatorMock.Setup(s => s.SimulateDispenseAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        
        await _controller.DispenseChangeAsync(1000, false, onComplete);
        onCompleteCalled.ShouldBeTrue();
        lastError.ShouldBe(ErrorCode.Success);

        // Async mode
        onCompleteCalled = false;
        await _controller.DispenseChangeAsync(1000, true, onComplete);
        await Task.Delay(200);
        onCompleteCalled.ShouldBeTrue();

        // Error: Busy
        var tcs = new TaskCompletionSource();
        _simulatorMock.Setup(s => s.SimulateDispenseAsync(It.IsAny<CancellationToken>())).Returns(tcs.Task);
        var task = _controller.DispenseChangeAsync(1000, false, (e, ex) => {});
        await Task.Delay(100);
        await Should.ThrowAsync<PosControlException>(async () => await _controller.DispenseChangeAsync(1000, false, (e, ex) => {}));
        tcs.SetResult();
        _controller.ClearOutput();

        // Error: Disconnected
        _hardwareStatusManager.SetConnected(false);
        await Should.ThrowAsync<PosControlException>(async () => await _controller.DispenseChangeAsync(1000, false, (e, ex) => {}));
        _hardwareStatusManager.SetConnected(true);
    }

    [Fact]
    public void CashCountParser_Branches()
    {
        var keys = new List<DenominationKey> { 
            new DenominationKey(100, CurrencyCashType.Coin, "JPY"),
            new DenominationKey(1000, CurrencyCashType.Bill, "JPY")
        };
        CashCountParser.Parse("100:10", keys, 1).Count().ShouldBe(1);
        CashCountParser.Parse("100:10;1000:5", keys, 1).Count().ShouldBe(2);
        CashCountParser.Parse(" ", keys, 1).ShouldBeEmpty();
        
        // Error cases
        Should.Throw<ArgumentException>(() => CashCountParser.Parse("100:10;1000:5;2000:1", keys, 1)); // Too many sections
        
        var ambiguousKeys = new List<DenominationKey> {
            new DenominationKey(100, CurrencyCashType.Coin, "JPY"),
            new DenominationKey(100, CurrencyCashType.Bill, "JPY")
        };
        Should.Throw<ArgumentException>(() => CashCountParser.Parse("100:1", ambiguousKeys, 1)); // Ambiguous
    }

    [Fact]
    public void UposHelpers_Coverage()
    {
        UposCurrencyHelper.GetCurrencyFactor("USD").ShouldBe(100);
        UposCurrencyHelper.GetCurrencyFactor("JPY").ShouldBe(1);

        var cc = new CashCount(CashCountType.Bill, 1000, 10);
        var formatted = CashCountAdapter.FormatCashCounts(new[] { cc });
        formatted.ShouldContain("1000:10");
    }

    [Fact]
    public async Task InternalSimulator_AllMethods()
    {
        using var device = new InternalSimulatorCashChanger();
        device.Open();
        device.Claim(1000);
        device.DeviceEnabled = true;

        device.HardwareStatus.SetConnected(true);
        device.CurrencyCode = "JPY";

        // Deposits
        device.BeginDeposit();
        device.FixDeposit();
        device.EndDeposit(CashDepositAction.NoChange);

        // Inventory
        device.AdjustCashCounts("1000:10");
        var ccStr = "";
        var disc = false;
        device.ReadCashCounts(ref ccStr, ref disc);
        ccStr.ShouldContain("1000:10");

        // Stats & Health
        device.RetrieveStatistics(new[] { "test" });
        device.CheckHealth(HealthCheckLevel.Internal);
        device.DirectIO(0, 0, "");

        device.DeviceEnabled = false;
        device.Release();
        device.Close();
    }

    [Fact]
    public async Task ScriptCommandHandlers_Coverage()
    {
        var logger = new Mock<ILogger>().Object;
        var context = new ScriptExecutionContext();

        // Enable
        var enableHandler = new EnableCommandHandler(_hardwareStatusManager);
        await enableHandler.ExecuteAsync(new ScriptCommand { Op = "enable", Value = "true" }, context, logger, null);
        _hardwareStatusManager.IsConnected.Value.ShouldBeTrue();

        // InjectError
        var injectHandler = new InjectErrorCommandHandler(_hardwareStatusManager);
        await injectHandler.ExecuteAsync(new ScriptCommand { Op = "inject-error", Error = "overlap" }, context, logger, null);
        _hardwareStatusManager.IsOverlapped.Value.ShouldBeTrue();
        
        await injectHandler.ExecuteAsync(new ScriptCommand { Op = "inject-error", Error = "device", ErrorCode = 1, ErrorCodeExtended = 2 }, context, logger, null);
        
        await injectHandler.ExecuteAsync(new ScriptCommand { Op = "inject-error", Error = "reset" }, context, logger, null);
        _hardwareStatusManager.IsOverlapped.Value.ShouldBeFalse();
        
        await injectHandler.ExecuteAsync(new ScriptCommand { Op = "inject-error", Error = "jam", Location = "Entrance" }, context, logger, null);
        _hardwareStatusManager.IsJammed.Value.ShouldBeTrue();

        await injectHandler.ExecuteAsync(new ScriptCommand { Op = "inject-error", Error = "unknown" }, context, logger, null);
        _hardwareStatusManager.IsJammed.Value.ShouldBeFalse();
        
        // Open (already connected in constructor, but call again)
        var openHandler = new OpenCommandHandler(_hardwareStatusManager);
        await openHandler.ExecuteAsync(new ScriptCommand { Op = "open" }, context, logger, null);
    }

    [Fact]
    public void Mediator_Coverage()
    {
        using var device = new InternalSimulatorCashChanger();
        var mediator = device.Context.Mediator;
        mediator.SetSuccess();
        mediator.ResultCode.ShouldBe((int)ErrorCode.Success);
        
        mediator.SetFailure(ErrorCode.Illegal, 100);
        mediator.ResultCode.ShouldBe((int)ErrorCode.Illegal);
        mediator.ResultCodeExtended.ShouldBe(100);

        mediator.SkipStateVerification = true;
        mediator.VerifyState(true, true, true); 
    }
}
