using CashChangerSimulator.Core.Models;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace CashChangerSimulator.Device.Virtual.Services.ScriptCommands;

/// <summary>assert コマンド: 実行時の状態をアサーションします。.</summary>
public class AssertCommandHandler(Inventory inventory) : IScriptCommandHandler
{
    /// <summary>Gets コマンド名を取得します。.</summary>
    public string OpName => "ASSERT";

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

        var target = cmd.Target?.ToUpperInvariant();
        var expected = ScriptExecutionService.ResolveValue(cmd.Value, context);
        logger.ZLogInformation($"Asserting {target} == {expected}");
        if (target == "INVENTORY")
        {
            var denomValue = ScriptExecutionService.ResolveValue(cmd.Denom ?? 0, context);
            var isCoin = string.Equals(cmd.Type, "coin", StringComparison.OrdinalIgnoreCase);
            var key = new DenominationKey(denomValue, isCoin ? CurrencyCashType.Coin : CurrencyCashType.Bill, cmd.Currency ?? "JPY");
            var count = inventory.GetCount(key);
            if (count != expected)
            {
                throw new InvalidOperationException($"Assert failed: Inventory count for {key} is {count}, expected {expected}");
            }
        }

        return Task.CompletedTask;
    }
}
