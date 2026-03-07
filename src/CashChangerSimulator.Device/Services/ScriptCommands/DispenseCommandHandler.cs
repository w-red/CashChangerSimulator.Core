using Microsoft.Extensions.Logging;
using ZLogger;

namespace CashChangerSimulator.Device.Services.ScriptCommands;

/// <summary>dispense コマンド: 出金を実行します。</summary>
public class DispenseCommandHandler(DispenseController dispenseController) : IScriptCommandHandler
{
    /// <summary>コマンド名を取得します。</summary>
    public string OpName => "dispense";

    /// <summary>スクリプトコマンドを実行します。</summary>
    public async Task ExecuteAsync(ScriptCommand cmd, ScriptExecutionContext context, ILogger logger, Action<string>? onProgress)
    {
        var dispenseValue = ScriptExecutionService.ResolveValue(cmd.Value, context);
        logger.ZLogDebug($"Dispense: {dispenseValue}");
        await dispenseController.DispenseChangeAsync(dispenseValue, false, (c, ex) => { });
    }
}
