using Microsoft.Extensions.Logging;
using ZLogger;

namespace CashChangerSimulator.Device.Virtual.Services.ScriptCommands;

/// <summary>dispense コマンド: 出金を実行します。</summary>
/// <param name="dispenseController">出金管理コントローラー。</param>
public class DispenseCommandHandler(
    DispenseController dispenseController)
    : IScriptCommandHandler
{
    /// <inheritdoc/>
    public ScriptCommandType OpName =>
        ScriptCommandType.Dispense;

    /// <inheritdoc/>
    public async Task ExecuteAsync(
        ScriptCommand cmd,
        ScriptExecutionContext context,
        ILogger logger,
        Action<string>? onProgress)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        var dispenseValue = ScriptExecutionService.ResolveValue(cmd.Value, context);
        logger?.ZLogDebug($"Dispense: {dispenseValue}");

        await dispenseController.DispenseChangeAsync(dispenseValue, false).ConfigureAwait(false);
    }
}
