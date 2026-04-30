using CashChangerSimulator.Core.Models;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace CashChangerSimulator.Device.Virtual.Services.ScriptCommands;

/// <summary>trackdeposit コマンド: 金種を投入シミュレーションします。</summary>
/// <param name="depositController">入金管理コントローラー。</param>
/// <param name="timeProvider">時刻プロバイダー。</param>
public class TrackDepositCommandHandler(
    DepositController depositController,
    TimeProvider timeProvider)
    : IScriptCommandHandler
{
    /// <inheritdoc/>
    public ScriptCommandType OpName =>
        ScriptCommandType.TrackDeposit;

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

        var cashType = ScriptCommandType.ToCurrencyCashType(cmd.Type);
        var value = ScriptExecutionService.ResolveValue(cmd.Value, context);
        var count = cmd.Count != null ? ScriptExecutionService.ResolveValue(cmd.Count, context) : 1;
        var key = new DenominationKey(value, cashType, cmd.Currency ?? "JPY");
        logger?.ZLogDebug($"TrackDeposit: {key} (Count: {count})");

        depositController.TrackBulkDeposit(new Dictionary<DenominationKey, int> { { key, count } });
        await Task.Delay(TimeSpan.FromMilliseconds(250), timeProvider).ConfigureAwait(false);
    }
}
