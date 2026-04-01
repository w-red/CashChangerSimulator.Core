using CashChangerSimulator.Core.Managers;
using Microsoft.Extensions.Logging;

namespace CashChangerSimulator.Device.Services.ScriptCommands;

public class EnableCommandHandler(HardwareStatusManager hardwareStatusManager) : IScriptCommandHandler
{
    public string OpName => "enable";

    public Task ExecuteAsync(ScriptCommand cmd, ScriptExecutionContext context, ILogger logger, Action<string>? onProgress)
    {
        hardwareStatusManager.SetConnected(true); // Open status
        // Actual enabling logic is in LifecycleManager, but here we can just set the property if we have access
        // For simplicity in script, we can just trigger the actual HardwareStatusManager or LifecycleManager
        return Task.CompletedTask;
    }
}
