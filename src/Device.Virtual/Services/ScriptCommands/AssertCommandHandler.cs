using CashChangerSimulator.Core.Models;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace CashChangerSimulator.Device.Virtual.Services.ScriptCommands;

/// <summary>assert コマンド: 実行時の状態をアサーションします。</summary>
/// <param name="inventory">在庫管理インスタンス。</param>
public class AssertCommandHandler(
    Inventory inventory)
    : IScriptCommandHandler
{
    /// <inheritdoc/>
    public ScriptCommandType OpName =>
        ScriptCommandType.Assert;

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

        var target = ScriptTargetType.FromString(cmd.Target);
        var expected = ScriptExecutionService.ResolveValue(cmd.Value, context);
        logger?.ZLogInformation($"Asserting {target} == {expected}");

        if (target == ScriptTargetType.Inventory)
        {
            var denomValue = ScriptExecutionService.ResolveValue(cmd.Denom ?? 0, context);
            var cashType = ScriptCommandType.ToCurrencyCashType(cmd.Type);
            var key = new DenominationKey(denomValue, cashType, cmd.Currency ?? "JPY");
            var count = inventory.GetCount(key);
            if (count != expected)
            {
                throw new InvalidOperationException($"Assert failed: Inventory count for {key} is {count}, expected {expected}");
            }
        }

        return Task.CompletedTask;
    }
}
