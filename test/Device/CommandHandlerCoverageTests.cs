using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device.Virtual.Services;
using CashChangerSimulator.Device.Virtual.Services.ScriptCommands;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace CashChangerSimulator.Tests.Device;

public class CommandHandlerCoverageTests
{
    /// <summary>無効なターゲットが指定された際のアサート制御が適切にハンドリングされることを検証する。</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task AssertCommandHandlerWhenTargetIsInvalidShouldHandleGracefully()
    {
        var handler = new AssertCommandHandler(Inventory.Create());
        var cmd = new ScriptCommand { Target = "UNKNOWN_TARGET", Value = "Value" };
        var context = new ScriptExecutionContext();

        var exception = await Record.ExceptionAsync(() => handler.ExecuteAsync(cmd, context, NullLogger.Instance, null));
        exception.ShouldNotBeNull();
    }

    /// <summary>無効なアクションが指定された際の有効化制御が例外なく処理されることを検証する。</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Fact]
    public async Task EnableCommandHandlerWhenActionIsInvalidShouldDoNothingOrLog()
    {
        var handler = new EnableCommandHandler(HardwareStatusManager.Create());
        var cmd = new ScriptCommand { Action = "INVALID_ACTION" };
        var context = new ScriptExecutionContext();

        await Should.NotThrowAsync(() => handler.ExecuteAsync(cmd, context, NullLogger.Instance, null));
    }
}
