using Microsoft.Extensions.Logging;
using ZLogger;

namespace CashChangerSimulator.Device.Virtual.Services.ScriptCommands;

/// <summary>set コマンド: 変数に値を格納します。</summary>
public class SetCommandHandler : IScriptCommandHandler
{
    /// <summary>コマンド名を取得します。</summary>
    public string OpName => "SET";

    /// <summary>スクリプトコマンドを実行します。</summary>
    /// <param name="cmd">コマンド。</param>
    /// <param name="context">実行コンテキスト。</param>
    /// <param name="logger">ロガー。</param>
    /// <param name="onProgress">進行状況を通知するコールバック。</param>
    /// <returns>非同期タスク。</returns>
    public Task ExecuteAsync(ScriptCommand cmd, ScriptExecutionContext context, ILogger logger, Action<string>? onProgress)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        if (!string.IsNullOrEmpty(cmd.Variable))
        {
            context.Variables[cmd.Variable] = cmd.Value;
            logger.ZLogInformation($"Set variable: {cmd.Variable} = {cmd.Value}");
        }

        return Task.CompletedTask;
    }
}
