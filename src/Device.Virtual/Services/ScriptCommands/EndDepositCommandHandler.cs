using CashChangerSimulator.Core.Models;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace CashChangerSimulator.Device.Virtual.Services.ScriptCommands;

/// <summary>enddeposit コマンド: 入金を終了します(仮想デバイス)。</summary>
public class EndDepositCommandHandler(DepositController depositController) : IScriptCommandHandler
{
    /// <summary>コマンド名を取得します。</summary>
    public ScriptCommandType OpName => ScriptCommandType.EndDeposit;

    /// <summary>スクリプトコマンドを実行します。</summary>
    /// <param name="cmd">コマンド。</param>
    /// <param name="context">実行コンテキスト。</param>
    /// <param name="logger">ロガー。</param>
    /// <param name="onProgress">進行状況を通知するコールバック。</param>
    /// <returns>非同期タスク。</returns>
    public async Task ExecuteAsync(ScriptCommand cmd, ScriptExecutionContext context, ILogger logger, Action<string>? onProgress)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        var action = cmd.Action?.ToUpperInvariant() switch
        {
            "REPAY" => DepositAction.Repay,
            "CHANGE" => DepositAction.Change,
            "NOCHANGE" => DepositAction.NoChange,
            "STORE" => DepositAction.NoChange,
            _ => DepositAction.NoChange
        };
        logger?.ZLogDebug($"EndDeposit Action: {action}");

        await depositController.EndDepositAsync(action).ConfigureAwait(false);
    }
}
