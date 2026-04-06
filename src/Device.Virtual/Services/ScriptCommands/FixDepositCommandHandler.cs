using Microsoft.Extensions.Logging;

namespace CashChangerSimulator.Device.Virtual.Services.ScriptCommands;

/// <summary>fixdeposit コマンド: 入金を確定します。.</summary>
public class FixDepositCommandHandler(DepositController depositController) : IScriptCommandHandler
{
    /// <summary>Gets コマンド名を取得します。.</summary>
    public string OpName => "FIXDEPOSIT";

    /// <summary>スクリプトコマンドを実行します。.</summary>
    /// <param name="cmd">コマンド。.</param>
    /// <param name="context">実行コンテキスト。.</param>
    /// <param name="logger">ロガー。.</param>
    /// <param name="onProgress">進行状況を通知するコールバック。.</param>
    /// <returns>非同期タスク。.</returns>
    public Task ExecuteAsync(ScriptCommand cmd, ScriptExecutionContext context, ILogger logger, Action<string>? onProgress)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        depositController.FixDeposit();
        return Task.CompletedTask;
    }
}
