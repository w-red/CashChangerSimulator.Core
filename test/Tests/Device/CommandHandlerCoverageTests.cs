using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device.Virtual.Services;
using CashChangerSimulator.Device.Virtual.Services.ScriptCommands;
using Shouldly;
using Microsoft.Extensions.Logging.Abstractions;
using CashChangerSimulator.Core.Managers;

namespace CashChangerSimulator.Tests.Device;

public class CommandHandlerCoverageTests
{
    /// <summary>無効なターゲットが指定された際のアサート制御が適切にハンドリングされることを検証する。</summary>
    [Fact]
    public async Task AssertCommandHandlerWhenTargetIsInvalidShouldHandleGracefully()
    {
        var handler = new AssertCommandHandler(new Inventory());
        var cmd = new ScriptCommand { Target = "UNKNOWN_TARGET", Value = "Value" };
        var context = new ScriptExecutionContext();
        
        var exception = await Record.ExceptionAsync(() => handler.ExecuteAsync(cmd, context, NullLogger.Instance, null));
        exception.ShouldNotBeNull();
    }

    /// <summary>無効なアクションが指定された際の有効化制御が例外なく処理されることを検証する。</summary>
    [Fact]
    public async Task EnableCommandHandlerWhenActionIsInvalidShouldDoNothingOrLog()
    {
        var handler = new EnableCommandHandler(new HardwareStatusManager());
        var cmd = new ScriptCommand { Action = "INVALID_ACTION" };
        var context = new ScriptExecutionContext();
        
        await Should.NotThrowAsync(() => handler.ExecuteAsync(cmd, context, NullLogger.Instance, null));
    }
}
