using CashChangerSimulator.Core.Managers;
using Microsoft.Extensions.Logging;

namespace CashChangerSimulator.Device.Virtual.Services.ScriptCommands;

/// <summary>enable コマンド: デバイスを有効状態にします。</summary>
/// <param name="hardwareStatusManager">ハードウェア状態管理インスタンス。</param>
public class EnableCommandHandler(
    HardwareStatusManager hardwareStatusManager)
    : IScriptCommandHandler
{
    /// <inheritdoc/>
    public ScriptCommandType OpName =>
        ScriptCommandType.Enable;

    /// <inheritdoc/>
    public Task ExecuteAsync(
        ScriptCommand cmd,
        ScriptExecutionContext context,
        ILogger logger,
        Action<string>? onProgress)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        hardwareStatusManager.Input.DeviceEnabled.Value = true;
        return Task.CompletedTask;
    }
}
