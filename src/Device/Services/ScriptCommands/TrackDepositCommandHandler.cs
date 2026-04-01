using CashChangerSimulator.Core.Models;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace CashChangerSimulator.Device.Services.ScriptCommands;

/// <summary>trackdeposit コマンド: 金種を投入シミュレーションします。</summary>
public class TrackDepositCommandHandler(DepositController depositController) : IScriptCommandHandler
{
    /// <summary>コマンド名を取得します。</summary>
    public string OpName => "trackdeposit";

    /// <summary>スクリプトコマンドを実行します。</summary>
    public async Task ExecuteAsync(ScriptCommand cmd, ScriptExecutionContext context, ILogger logger, Action<string>? onProgress)
    {
        var type = cmd.Type?.ToLower() == "coin" ? CurrencyCashType.Coin : CurrencyCashType.Bill;
        var value = ScriptExecutionService.ResolveValue(cmd.Value, context);
        var key = new DenominationKey(value, type, cmd.Currency ?? "JPY");
        logger.ZLogDebug($"TrackDeposit: {key} (Count: {cmd.Count})");
        depositController.TrackBulkDeposit(new Dictionary<DenominationKey, int> { { key, cmd.Count } });
        await Task.Delay(250);
    }
}
