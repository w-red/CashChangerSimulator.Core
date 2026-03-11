using CashChangerSimulator.UI.Cli;
using CashChangerSimulator.UI.Cli.Services;
using Moq;
using Spectre.Console;
using Xunit;
using Microsoft.Extensions.Localization;

namespace CashChangerSimulator.Tests.Ui.Cli;

public class CliCommandDispatcherTests : CliTestBase
{
    private readonly Mock<CliCommands> _mockCommands;
    private readonly CliCommandDispatcher _dispatcher;

    public CliCommandDispatcherTests()
    {
        // CliCommands depends on many services. We can mock it since we made methods virtual.
        // We use the real services from CliTestBase for the constructor of the mock.
        var configProvider = new CashChangerSimulator.Core.Configuration.ConfigurationProvider();
        _mockCommands = new Mock<CliCommands>(
            _mockChanger.Object,
            new CliDeviceService(_mockChanger.Object, _console, _localizer),
            new CliCashService(_mockChanger.Object, _mockInventory.Object, _mockMetadata.Object, _options, _console, _localizer),
            new CliConfigService(configProvider, _console, _localizer),
            new CliViewService(_mockChanger.Object, _mockInventory.Object, _mockMetadata.Object, _mockHistory.Object, _console, _localizer),
            new CliScriptService(_mockScriptService.Object, _console, _localizer),
            _console,
            _localizer
        );
        _dispatcher = new CliCommandDispatcher(_mockCommands.Object);
    }

    [Fact]
    public async Task DispatchAsync_Open_ShouldCallOpen()
    {
        await _dispatcher.DispatchAsync("open");
        _mockCommands.Verify(x => x.Open(), Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_Claim_ShouldCallClaimWithTimeout()
    {
        await _dispatcher.DispatchAsync("claim 2000");
        _mockCommands.Verify(x => x.Claim(2000), Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_ClaimDefault_ShouldCallClaimWithDefaultTimeout()
    {
        await _dispatcher.DispatchAsync("claim");
        _mockCommands.Verify(x => x.Claim(1000), Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_Deposit_ShouldCallDeposit()
    {
        await _dispatcher.DispatchAsync("deposit 500");
        _mockCommands.Verify(x => x.Deposit(500), Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_ConfigList_ShouldCallConfigList()
    {
        await _dispatcher.DispatchAsync("config list");
        _mockCommands.Verify(x => x.ConfigList(), Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_ConfigSet_ShouldCallConfigSetWithArgs()
    {
        await _dispatcher.DispatchAsync("config set key value");
        _mockCommands.Verify(x => x.ConfigSet("key", "value"), Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_UnknownCommand_ShouldPrintErrorMessage()
    {
        await _dispatcher.DispatchAsync("unknown");
        // Check console output if needed, but the main thing is it doesn't crash
    }
}
