using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device.Virtual.Services;
using CashChangerSimulator.Device.Virtual.Services.ScriptCommands;
using Shouldly;
using Microsoft.Extensions.Logging.Abstractions;
using CashChangerSimulator.Core.Managers;

namespace CashChangerSimulator.Tests.Device;

public class CommandHandlerCoverageTests
{
    [Fact]
    public async Task AssertCommandHandler_WhenTargetIsInvalid_ShouldHandleGracefully()
    {
        var handler = new AssertCommandHandler(new Inventory());
        var cmd = new ScriptCommand { Target = "UNKNOWN_TARGET", Value = "Value" };
        var context = new ScriptExecutionContext();
        
        var exception = await Record.ExceptionAsync(() => handler.ExecuteAsync(cmd, context, NullLogger.Instance, null));
        exception.ShouldNotBeNull();
    }

    [Fact]
    public async Task EnableCommandHandler_WhenActionIsInvalid_ShouldDoNothingOrLog()
    {
        var handler = new EnableCommandHandler(new HardwareStatusManager());
        var cmd = new ScriptCommand { Action = "INVALID_ACTION" };
        var context = new ScriptExecutionContext();
        
        await Should.NotThrowAsync(() => handler.ExecuteAsync(cmd, context, NullLogger.Instance, null));
    }
}
