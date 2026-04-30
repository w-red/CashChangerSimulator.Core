using Microsoft.Extensions.Logging;

namespace CashChangerSimulator.Device.Virtual.Services.ScriptCommands;

/// <summary>fixdeposit コマンド: 入金を確定します。</summary>
/// <param name="depositController">入金管理コントローラー。</param>
public class FixDepositCommandHandler(
    DepositController depositController)
    : IScriptCommandHandler
{
    /// <inheritdoc/>
    public ScriptCommandType OpName =>
        ScriptCommandType.FixDeposit;

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

        depositController.FixDeposit();
        return Task.CompletedTask;
    }
}
