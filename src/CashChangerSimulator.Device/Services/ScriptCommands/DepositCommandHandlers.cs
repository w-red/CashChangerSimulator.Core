using CashChangerSimulator.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using ZLogger;

namespace CashChangerSimulator.Device.Services.ScriptCommands;

/// <summary>begindeposit コマンド: 入金受付を開始します。</summary>
public class BeginDepositCommandHandler(DepositController depositController) : IScriptCommandHandler
{
    public string OpName => "begindeposit";

    public Task ExecuteAsync(ScriptCommand cmd, ScriptExecutionContext context, ILogger logger, Action<string>? onProgress)
    {
        depositController.BeginDeposit();
        return Task.CompletedTask;
    }
}

/// <summary>trackdeposit コマンド: 金種を投入シミュレーションします。</summary>
public class TrackDepositCommandHandler(DepositController depositController) : IScriptCommandHandler
{
    public string OpName => "trackdeposit";

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

/// <summary>fixdeposit コマンド: 入金を確定します。</summary>
public class FixDepositCommandHandler(DepositController depositController) : IScriptCommandHandler
{
    public string OpName => "fixdeposit";

    public Task ExecuteAsync(ScriptCommand cmd, ScriptExecutionContext context, ILogger logger, Action<string>? onProgress)
    {
        depositController.FixDeposit();
        return Task.CompletedTask;
    }
}

/// <summary>enddeposit コマンド: 入金を終了します。</summary>
public class EndDepositCommandHandler(DepositController depositController) : IScriptCommandHandler
{
    public string OpName => "enddeposit";

    public Task ExecuteAsync(ScriptCommand cmd, ScriptExecutionContext context, ILogger logger, Action<string>? onProgress)
    {
        var action = cmd.Action?.ToLower() switch
        {
            "repay" => CashDepositAction.Repay,
            _ => CashDepositAction.NoChange
        };
        logger.ZLogDebug($"EndDeposit Action: {action}");
        depositController.EndDeposit(action);
        return Task.CompletedTask;
    }
}

/// <summary>dispense コマンド: 出金を実行します。</summary>
public class DispenseCommandHandler(DispenseController dispenseController) : IScriptCommandHandler
{
    public string OpName => "dispense";

    public async Task ExecuteAsync(ScriptCommand cmd, ScriptExecutionContext context, ILogger logger, Action<string>? onProgress)
    {
        var dispenseValue = ScriptExecutionService.ResolveValue(cmd.Value, context);
        logger.ZLogDebug($"Dispense: {dispenseValue}");
        await dispenseController.DispenseChangeAsync(dispenseValue, false, (c, ex) => { });
    }
}

/// <summary>assert コマンド: 実行時の状態をアサーションします。</summary>
public class AssertCommandHandler(Inventory inventory) : IScriptCommandHandler
{
    public string OpName => "assert";

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
