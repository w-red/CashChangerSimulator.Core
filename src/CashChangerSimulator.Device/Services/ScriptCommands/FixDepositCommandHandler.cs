using Microsoft.Extensions.Logging;

namespace CashChangerSimulator.Device.Services.ScriptCommands;

/// <summary>fixdeposit コマンド: 入金を確定します。</summary>
public class FixDepositCommandHandler(DepositController depositController) : IScriptCommandHandler
{
    /// <summary>コマンド名を取得します。</summary>
    public string OpName => "fixdeposit";

    /// <summary>スクリプトコマンドを実行します。</summary>
    public Task ExecuteAsync(ScriptCommand cmd, ScriptExecutionContext context, ILogger logger, Action<string>? onProgress)
    {
        depositController.FixDeposit();
        return Task.CompletedTask;
    }
}
