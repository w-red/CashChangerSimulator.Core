using Microsoft.Extensions.Logging;
using ZLogger;

namespace CashChangerSimulator.Device.Services.ScriptCommands;

/// <summary>set コマンド: 変数に値を格納します。</summary>
public class SetCommandHandler : IScriptCommandHandler
{
    /// <summary>コマンド名を取得します。</summary>
    public string OpName => "set";

    /// <summary>スクリプトコマンドを実行します。</summary>
    public Task ExecuteAsync(ScriptCommand cmd, ScriptExecutionContext context, ILogger logger, Action<string>? onProgress)
    {
        if (!string.IsNullOrEmpty(cmd.Variable))
        {
            context.Variables[cmd.Variable] = cmd.Value;
            logger.ZLogInformation($"Set variable: {cmd.Variable} = {cmd.Value}");
        }
        return Task.CompletedTask;
    }
}
