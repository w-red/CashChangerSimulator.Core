using Microsoft.Extensions.Logging;
using ZLogger;

namespace CashChangerSimulator.Device.Virtual.Services.ScriptCommands;

/// <summary>enddeposit コマンド: 入金を終了します(仮想デバイス)。</summary>
/// <param name="depositController">入金管理コントローラー。</param>
public class EndDepositCommandHandler(
    DepositController depositController)
    : IScriptCommandHandler
{
    /// <inheritdoc/>
    public ScriptCommandType OpName =>
        ScriptCommandType.EndDeposit;

    /// <inheritdoc/>
    public async Task ExecuteAsync(
        ScriptCommand cmd,
        ScriptExecutionContext context,
        ILogger logger,
        Action<string>? onProgress)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        var action = ScriptCommandType.ToDepositAction(cmd.Action);
        logger?.ZLogDebug($"EndDeposit Action: {action}");

        await depositController.EndDepositAsync(action).ConfigureAwait(false);
    }
}
