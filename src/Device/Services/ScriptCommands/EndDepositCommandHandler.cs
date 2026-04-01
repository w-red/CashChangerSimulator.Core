using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using ZLogger;

namespace CashChangerSimulator.Device.Services.ScriptCommands;

/// <summary>enddeposit コマンド: 入金を終了します。</summary>
public class EndDepositCommandHandler(DepositController depositController) : IScriptCommandHandler
{
    /// <summary>コマンド名を取得します。</summary>
    public string OpName => "enddeposit";

    /// <summary>スクリプトコマンドを実行します。</summary>
    public Task ExecuteAsync(ScriptCommand cmd, ScriptExecutionContext context, ILogger logger, Action<string>? onProgress)
    {
        var action = cmd.Action?.ToLower() switch
        {
            "repay" => CashDepositAction.Repay,
            _ => CashDepositAction.NoChange
        };
        logger.ZLogDebug($"EndDeposit Action: {action}");
        depositController.EndDeposit(action);
        return Task.CompletedTask;
    }
}
