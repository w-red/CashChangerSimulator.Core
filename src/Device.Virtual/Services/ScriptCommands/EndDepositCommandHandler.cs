using Microsoft.Extensions.Logging;
using ZLogger;

namespace CashChangerSimulator.Device.Virtual.Services.ScriptCommands;

/// <summary>enddeposit コマンド: 入金を終了します（仮想デバイス）。</summary>
public class EndDepositCommandHandler(DepositController depositController) : IScriptCommandHandler
{
    /// <summary>コマンド名を取得します。</summary>
    public string OpName => "enddeposit";

    /// <summary>スクリプトコマンドを実行します。</summary>
    public Task ExecuteAsync(ScriptCommand cmd, ScriptExecutionContext context, ILogger logger, Action<string>? onProgress)
    {
        var action = cmd.Action?.ToLower() switch
        {
            "repay" => DepositAction.Repay,
            _ => DepositAction.Store
        };
        logger.ZLogDebug($"EndDeposit Action: {action}");
        depositController.EndDeposit(action);
        return Task.CompletedTask;
    }
}
