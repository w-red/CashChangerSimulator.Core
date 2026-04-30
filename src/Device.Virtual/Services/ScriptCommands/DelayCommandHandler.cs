using Microsoft.Extensions.Logging;
using ZLogger;

namespace CashChangerSimulator.Device.Virtual.Services.ScriptCommands;

/// <summary>delay コマンド: 指定ミリ秒待機します。</summary>
/// <param name="timeProvider">時刻プロバイダー。</param>
public class DelayCommandHandler(
    TimeProvider timeProvider)
    : IScriptCommandHandler
{
    /// <inheritdoc/>
    public ScriptCommandType OpName =>
        ScriptCommandType.Delay;

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

        var ms = ScriptExecutionService.ResolveValue(cmd.Value, context);
        logger?.ZLogDebug($"Delaying for {ms}ms");

        await Task.Delay(TimeSpan.FromMilliseconds(ms), timeProvider).ConfigureAwait(false);
    }
}
