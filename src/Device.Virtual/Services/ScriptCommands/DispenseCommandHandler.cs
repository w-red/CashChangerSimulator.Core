using Microsoft.Extensions.Logging;
using ZLogger;

namespace CashChangerSimulator.Device.Virtual.Services.ScriptCommands;

/// <summary>dispense コマンド: 出金を実行します。</summary>
public class DispenseCommandHandler(
    DispenseController dispenseController)
    : IScriptCommandHandler
{
    /// <summary>コマンド名を取得します。</summary>
    public ScriptCommandType OpName =>
        ScriptCommandType.Dispense;

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

        var dispenseValue = ScriptExecutionService.ResolveValue(cmd.Value, context);
        logger?.ZLogDebug($"Dispense: {dispenseValue}");

        await dispenseController.DispenseChangeAsync(dispenseValue, false).ConfigureAwait(false);
    }
}
