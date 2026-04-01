using Microsoft.Extensions.Logging;
using ZLogger;

namespace CashChangerSimulator.Device.Services.ScriptCommands;

/// <summary>delay コマンド: 指定ミリ秒待機します。</summary>
public class DelayCommandHandler : IScriptCommandHandler
{
    /// <summary>コマンド名を取得します。</summary>
    public string OpName => "delay";

    /// <summary>スクリプトコマンドを実行します。</summary>
    public async Task ExecuteAsync(ScriptCommand cmd, ScriptExecutionContext context, ILogger logger, Action<string>? onProgress)
    {
        var ms = ScriptExecutionService.ResolveValue(cmd.Value, context);
        logger.ZLogDebug($"Delaying for {ms}ms");
        await Task.Delay(ms);
    }
}
