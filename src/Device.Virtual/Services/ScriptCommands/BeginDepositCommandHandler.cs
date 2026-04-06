using Microsoft.Extensions.Logging;

namespace CashChangerSimulator.Device.Virtual.Services.ScriptCommands;

/// <summary>begindeposit コマンド: 入金受付を開始します。.</summary>
public class BeginDepositCommandHandler(DepositController depositController) : IScriptCommandHandler
{
    /// <summary>Gets コマンド名を取得します。.</summary>
    public string OpName => "BEGINDEPOSIT";

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

        depositController.BeginDeposit();
        return Task.CompletedTask;
    }
}
