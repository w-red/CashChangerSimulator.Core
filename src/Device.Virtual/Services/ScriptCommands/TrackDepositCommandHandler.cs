using CashChangerSimulator.Core.Models;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace CashChangerSimulator.Device.Virtual.Services.ScriptCommands;

/// <summary>trackdeposit コマンド: 金種を投入シミュレーションします。.</summary>
public class TrackDepositCommandHandler(DepositController depositController) : IScriptCommandHandler
{
    /// <summary>Gets コマンド名を取得します。.</summary>
    public string OpName => "TRACKDEPOSIT";

    /// <summary>スクリプトコマンドを実行します。.</summary>
    /// <param name="cmd">コマンド。.</param>
    /// <param name="context">実行コンテキスト。.</param>
    /// <param name="logger">ロガー。.</param>
    /// <param name="onProgress">進行状況を通知するコールバック。.</param>
    /// <returns>非同期タスク。.</returns>
    public async Task ExecuteAsync(ScriptCommand cmd, ScriptExecutionContext context, ILogger logger, Action<string>? onProgress)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        var isCoin = string.Equals(cmd.Type, "coin", StringComparison.OrdinalIgnoreCase);
        var type = isCoin ? CurrencyCashType.Coin : CurrencyCashType.Bill;
        var value = ScriptExecutionService.ResolveValue(cmd.Value, context);
        var count = cmd.Count != null ? ScriptExecutionService.ResolveValue(cmd.Count, context) : 1;
        var key = new DenominationKey(value, type, cmd.Currency ?? "JPY");
        logger.ZLogDebug($"TrackDeposit: {key} (Count: {count})");
        depositController.TrackBulkDeposit(new Dictionary<DenominationKey, int> { { key, count } });
        await Task.Delay(250).ConfigureAwait(false);
    }
}
