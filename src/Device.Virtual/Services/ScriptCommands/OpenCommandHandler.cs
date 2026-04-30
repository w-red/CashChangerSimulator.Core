using CashChangerSimulator.Core.Managers;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace CashChangerSimulator.Device.Virtual.Services.ScriptCommands;

/// <summary>open コマンド: デバイスを接続状態にします。</summary>
/// <param name="hardwareStatusManager">ハードウェア状態管理インスタンス。</param>
public class OpenCommandHandler(
    HardwareStatusManager hardwareStatusManager)
    : IScriptCommandHandler
{
    /// <inheritdoc/>
    public ScriptCommandType OpName =>
        ScriptCommandType.Open;

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

        logger?.ZLogInformation($"Opening device via script.");

        hardwareStatusManager.Input.IsConnected.Value = true;
        return Task.CompletedTask;
    }
}
