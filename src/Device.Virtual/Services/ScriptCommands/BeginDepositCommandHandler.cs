using Microsoft.Extensions.Logging;

namespace CashChangerSimulator.Device.Virtual.Services.ScriptCommands;

/// <summary>begindeposit コマンド: 入金受付を開始します。</summary>
/// <param name="depositController">入金管理コントローラー。</param>
public class BeginDepositCommandHandler(
    DepositController depositController)
    : IScriptCommandHandler
{
    /// <inheritdoc/>
    public ScriptCommandType OpName =>
        ScriptCommandType.BeginDeposit;

    /// <inheritdoc/>
    public Task ExecuteAsync(
        ScriptCommand cmd,
        ScriptExecutionContext context,
        ILogger logger,
        Action<string>? onProgress)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        depositController.BeginDeposit();
        return Task.CompletedTask;
    }
}
