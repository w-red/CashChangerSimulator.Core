using Microsoft.Extensions.Logging;

namespace CashChangerSimulator.Device.Services.ScriptCommands;

/// <summary>begindeposit コマンド: 入金受付を開始します。</summary>
public class BeginDepositCommandHandler(DepositController depositController) : IScriptCommandHandler
{
    /// <summary>コマンド名を取得します。</summary>
    public string OpName => "begindeposit";

    /// <summary>スクリプトコマンドを実行します。</summary>
    public Task ExecuteAsync(ScriptCommand cmd, ScriptExecutionContext context, ILogger logger, Action<string>? onProgress)
    {
        depositController.BeginDeposit();
        return Task.CompletedTask;
    }
}
