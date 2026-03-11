using CashChangerSimulator.UI.Cli;
using CashChangerSimulator.UI.Cli.Services;
using Moq;
using Spectre.Console;
using Xunit;
using Microsoft.Extensions.Localization;

namespace CashChangerSimulator.Tests.Ui.Cli;

public class CliInteractiveShellTests : CliTestBase
{
    private readonly Mock<ICliCommandDispatcher> _mockDispatcher;
    private readonly Mock<ILineReader> _mockReader;
    private readonly CliInteractiveShell _shell;

    public CliInteractiveShellTests()
    {
        _mockDispatcher = new Mock<ICliCommandDispatcher>();
        _mockReader = new Mock<ILineReader>();
        _shell = new CliInteractiveShell(
            _mockDispatcher.Object,
            _mockChanger.Object,
            _console,
            _localizer,
            _options,
            _mockReader.Object
        );
    }

    [Fact]
    public async Task RunAsync_ShouldProcessInputAndStop_WhenExitCommand()
    {
        // Setup mock sequence: "status", then "exit"
        _mockReader.SetupSequence(x => x.Read(It.IsAny<string>()))
            .Returns("status")
            .Returns("exit");

        // Mock ConfirmExit to return true
        // Note: ConfirmExit might be internal or private, we might need to expose it or mock IAnsiConsole.Confirm
        // But since we are using CliInteractiveShell, we can control exchanger state.
        _mockChanger.Setup(x => x.State).Returns(Microsoft.PointOfService.ControlState.Closed);

        await _shell.RunAsync();

        _mockDispatcher.Verify(x => x.DispatchAsync("status"), Times.Once);
        // "exit" should stop the loop, dispatcher might not be called for exit if handled in shell
    }
}
