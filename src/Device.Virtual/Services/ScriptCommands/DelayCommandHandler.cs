using Microsoft.Extensions.Logging;
using ZLogger;

namespace CashChangerSimulator.Device.Virtual.Services.ScriptCommands;

/// <summary>delay コマンド: 指定ミリ秒待機します。</summary>
public class DelayCommandHandler(
    TimeProvider timeProvider)
    : IScriptCommandHandler
{
    /// <summary>コマンド名を取得します。</summary>
    public ScriptCommandType OpName =>
        ScriptCommandType.Delay;

    /// <summary>スクリプトコマンドを実行します。</summary>
    /// <param name="cmd">コマンド。</param>
    /// <param name="context">実行コンテキスト。</param>
    /// <param name="logger">ロガー。</param>
    /// <param name="onProgress">進行状況を通知するコールバック。</param>
    /// <returns>非同期タスク。</returns>
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
