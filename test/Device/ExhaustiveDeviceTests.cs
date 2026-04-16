using CashChangerSimulator.Core.Configuration;
using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Services;
using CashChangerSimulator.Core.Transactions;
using CashChangerSimulator.Device.PosForDotNet;
using CashChangerSimulator.Device.Virtual;
using CashChangerSimulator.Device.Virtual.Services;
using CashChangerSimulator.Device.Virtual.Services.ScriptCommands;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.PointOfService;
using Moq;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

/// <summary>CashChangerSimulator.Device のカバレッジを 100% にするための網羅的テストクラス。</summary>
public class ExhaustiveDeviceTests : IDisposable
{
    private readonly Inventory inventory;
    private readonly CashChangerManager manager;
    private readonly HardwareStatusManager hardwareStatusManager;
    private readonly Mock<IDeviceSimulator> simulatorMock;
    private readonly FakeTimeProvider timeProvider;
    private readonly DispenseController controller;

    public ExhaustiveDeviceTests()
    {
        inventory = Inventory.Create();
        var history = new TransactionHistory();
        var configProvider = new ConfigurationProvider();
        manager = new CashChangerManager(inventory, history, null, null);
        hardwareStatusManager = HardwareStatusManager.Create();
        simulatorMock = new Mock<IDeviceSimulator>();
        timeProvider = new FakeTimeProvider();
        controller = new DispenseController(manager, inventory, configProvider, NullLoggerFactory.Instance, hardwareStatusManager, simulatorMock.Object);

        hardwareStatusManager.Input.IsConnected.Value = true;
    }

    public void Dispose()
    {
        controller.Dispose();
        hardwareStatusManager.Dispose();
    }

    [Fact]
    public async Task DispenseControllerFlows()
    {
        // Normal path
        var key = new DenominationKey(1000, CurrencyCashType.Bill, "JPY");
        inventory.SetCount(key, 10);
        simulatorMock.Setup(s => s.SimulateDispenseAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        await controller.DispenseChangeAsync(1000, false).ConfigureAwait(false);
        controller.LastErrorCode.ShouldBe(DeviceErrorCode.Success);

        // Async mode
        await controller.DispenseChangeAsync(1000, true).ConfigureAwait(false);
        timeProvider.Advance(TimeSpan.FromMilliseconds(200));
        controller.LastErrorCode.ShouldBe(DeviceErrorCode.Success);

        // Error: Busy
        var tcs = new TaskCompletionSource();
        simulatorMock.Setup(s => s.SimulateDispenseAsync(It.IsAny<CancellationToken>())).Returns(tcs.Task);
        var task = controller.DispenseChangeAsync(1000, false);
        timeProvider.Advance(TimeSpan.FromMilliseconds(100));
        await Should.ThrowAsync<DeviceException>(async () => await controller.DispenseChangeAsync(1000, false).ConfigureAwait(false)).ConfigureAwait(false);
        tcs.SetResult();
        controller.ClearOutput();

        // Error: Disconnected
        hardwareStatusManager.Input.IsConnected.Value = false;
        await Should.ThrowAsync<DeviceException>(async () => await controller.DispenseChangeAsync(1000, false).ConfigureAwait(false)).ConfigureAwait(false);
        hardwareStatusManager.Input.IsConnected.Value = true;
    }

    [Fact]
    public void CashCountParserBranches()
    {
        var keys = new List<DenominationKey>
        {
            new(100, CurrencyCashType.Coin, "JPY"),
            new(1000, CurrencyCashType.Bill, "JPY")
        };
        CashCountParser.Parse("100:10", keys, 1).Count().ShouldBe(1);
        CashCountParser.Parse("100:10;1000:5", keys, 1).Count().ShouldBe(2);
        CashCountParser.Parse(" ", keys, 1).ShouldBeEmpty();

        // Error cases
        Should.Throw<ArgumentException>(() => CashCountParser.Parse("100:10;1000:5;2000:1", keys, 1)); // Too many sections

        var ambiguousKeys = new List<DenominationKey>
        {
            new(100, CurrencyCashType.Coin, "JPY"),
            new(100, CurrencyCashType.Bill, "JPY")
        };
        Should.Throw<ArgumentException>(() => CashCountParser.Parse("100:1", ambiguousKeys, 1)); // Ambiguous
    }

    [Fact]
    public void UposHelpersCoverage()
    {
        UposCurrencyHelper.GetCurrencyFactor("USD").ShouldBe(100);
        UposCurrencyHelper.GetCurrencyFactor("JPY").ShouldBe(1);

        var cc = new CashCount(CashCountType.Bill, 1000, 10);
        var formatted = CashCountAdapter.FormatCashCounts(new[] { cc });
        formatted.ShouldContain("1000:10");
    }

    [Fact]
    public async Task InternalSimulatorAllMethods()
    {
        using var device = new InternalSimulatorCashChanger();
        device.Open();
        device.Claim(1000);
        device.DeviceEnabled = true;

        device.HardwareStatus.Input.IsConnected.Value = true;
        device.CurrencyCode = "JPY";

        // Deposits
        device.BeginDeposit();
        device.FixDeposit();
        device.EndDeposit(CashDepositAction.NoChange);

        // Inventory
        device.AdjustCashCounts("1000:10");
        var ccStr = string.Empty;
        var disc = false;
        device.ReadCashCounts(ref ccStr, ref disc);
        ccStr.ShouldContain("1000:10");

        // Stats & Health
        device.RetrieveStatistics(["test"]);
        device.CheckHealth(HealthCheckLevel.Internal);
        device.DirectIO(0, 0, string.Empty);

        device.DeviceEnabled = false;
        device.Release();
        device.Close();
    }

    [Fact]
    public async Task ScriptCommandHandlersCoverage()
    {
        var logger = new Mock<ILogger>().Object;
        var context = new ScriptExecutionContext();

        // Enable
        var enableHandler = new EnableCommandHandler(hardwareStatusManager);
        await enableHandler.ExecuteAsync(new ScriptCommand { Op = "enable", Value = "true" }, context, logger, null).ConfigureAwait(false);
        hardwareStatusManager.IsConnected.CurrentValue.ShouldBeTrue();

        // InjectError
        var injectHandler = new InjectErrorCommandHandler(hardwareStatusManager);
        await injectHandler.ExecuteAsync(new ScriptCommand { Op = "inject-error", Error = "overlap" }, context, logger, null).ConfigureAwait(false);
        hardwareStatusManager.IsOverlapped.CurrentValue.ShouldBeTrue();

        await injectHandler.ExecuteAsync(new ScriptCommand { Op = "inject-error", Error = "device", ErrorCode = 1, ErrorCodeExtended = 2 }, context, logger, null).ConfigureAwait(false);

        await injectHandler.ExecuteAsync(new ScriptCommand { Op = "inject-error", Error = "reset" }, context, logger, null).ConfigureAwait(false);
        hardwareStatusManager.IsOverlapped.CurrentValue.ShouldBeFalse();

        await injectHandler.ExecuteAsync(new ScriptCommand { Op = "inject-error", Error = "jam", Location = "Entrance" }, context, logger, null).ConfigureAwait(false);
        hardwareStatusManager.IsJammed.CurrentValue.ShouldBeTrue();

        await injectHandler.ExecuteAsync(new ScriptCommand { Op = "inject-error", Error = "unknown" }, context, logger, null).ConfigureAwait(false);
        hardwareStatusManager.IsJammed.CurrentValue.ShouldBeFalse();

        // Open (already connected in constructor, but call again)
        var openHandler = new OpenCommandHandler(hardwareStatusManager);
        await openHandler.ExecuteAsync(new ScriptCommand { Op = "open" }, context, logger, null).ConfigureAwait(false);
    }

    [Fact]
    public void MediatorCoverage()
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
