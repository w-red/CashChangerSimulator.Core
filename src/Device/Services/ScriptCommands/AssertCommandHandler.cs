using CashChangerSimulator.Core.Models;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace CashChangerSimulator.Device.Services.ScriptCommands;

/// <summary>assert コマンド: 実行時の状態をアサーションします。</summary>
public class AssertCommandHandler(Inventory inventory) : IScriptCommandHandler
{
    /// <summary>コマンド名を取得します。</summary>
    public string OpName => "assert";

    /// <summary>スクリプトコマンドを実行します。</summary>
    public Task ExecuteAsync(ScriptCommand cmd, ScriptExecutionContext context, ILogger logger, Action<string>? onProgress)
    {
        var target = cmd.Target?.ToLower();
        var expected = ScriptExecutionService.ResolveValue(cmd.Value, context);
        logger.ZLogInformation($"Asserting {target} == {expected}");
        if (target == "inventory")
        {
            var denomValue = ScriptExecutionService.ResolveValue(cmd.Denom ?? 0, context);
            var key = new DenominationKey(denomValue, cmd.Type?.ToLower() == "coin" ? CurrencyCashType.Coin : CurrencyCashType.Bill, cmd.Currency ?? "JPY");
            var count = inventory.GetCount(key);
            if (count != expected)
            {
                throw new Exception($"Assert failed: Inventory count for {key} is {count}, expected {expected}");
            }
        }
        return Task.CompletedTask;
    }
}
