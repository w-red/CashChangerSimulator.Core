using Microsoft.Extensions.Logging;
using ZLogger;

namespace CashChangerSimulator.Device.Virtual.Services.ScriptCommands;

/// <summary>set コマンド: 変数に値を格納します。</summary>
public class SetCommandHandler : IScriptCommandHandler
{
    /// <inheritdoc/>
    public ScriptCommandType OpName =>
        ScriptCommandType.Set;

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

        if (!string.IsNullOrEmpty(cmd.Variable))
        {
            context.Variables[cmd.Variable] = cmd.Value;
            logger?.ZLogInformation($"Set variable: {cmd.Variable} = {cmd.Value}");
        }

        return Task.CompletedTask;
    }
}
