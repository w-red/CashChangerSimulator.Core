using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using MoneyKind4Opos.Currencies.Interfaces;
using System.Text.Json;
using ZLogger;

namespace CashChangerSimulator.Device.Services;

/// <summary>
/// スクリプトデータに基づいてシミュレーターの操作を自動実行するサービス。
/// </summary>
public class ScriptExecutionService(
    DepositController depositController, 
    DispenseController dispenseController,
    Inventory inventory,
    HardwareStatusManager hardwareStatusManager) : IScriptExecutionService
{
    private readonly ILogger<ScriptExecutionService> _logger = LogProvider.CreateLogger<ScriptExecutionService>();

    private class ScriptExecutionContext
    {
        public Dictionary<string, object> Variables { get; } = new();
    }

    public async Task ExecuteScriptAsync(string json, Action<string>? onProgress = null)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var commands = JsonSerializer.Deserialize<List<ScriptCommand>>(json, options);
        if (commands == null)
        {
            _logger.ZLogError($"Failed to deserialize script JSON.");
            return;
        }

        _logger.ZLogInformation($"Starting script execution. Commands count: {commands.Count}");

        var context = new ScriptExecutionContext();
        await ExecuteCommandsInternalAsync(commands, context, onProgress);
        
        _logger.ZLogInformation($"Finished script execution.");
    }

    private async Task ExecuteCommandsInternalAsync(IEnumerable<ScriptCommand> commands, ScriptExecutionContext context, Action<string>? onProgress)
    {
        foreach (var cmd in commands)
        {
            await ExecuteCommandAsync(cmd, context, onProgress);
        }
    }

    private async Task ExecuteCommandAsync(ScriptCommand cmd, ScriptExecutionContext context, Action<string>? onProgress)
    {
        _logger.ZLogDebug($"Executing command: {cmd.Op}");
        onProgress?.Invoke(cmd.Op);
        
        switch (cmd.Op.ToLower())
        {
            case "open":
                _logger.ZLogInformation($"Opening device via script.");
                hardwareStatusManager.SetConnected(true);
                break;
            case "repeat":
                if (cmd.Commands != null)
                {
                    _logger.ZLogInformation($"Starting repeat loop: {cmd.Count} times.");
                    for (int i = 0; i < cmd.Count; i++)
                    {
                        await ExecuteCommandsInternalAsync(cmd.Commands, context, onProgress);
                    }
                    _logger.ZLogInformation($"Finished repeat loop.");
                }
                break;
            case "set":
                if (!string.IsNullOrEmpty(cmd.Variable))
                {
                    context.Variables[cmd.Variable] = cmd.Value;
                    _logger.ZLogInformation($"Set variable: {cmd.Variable} = {cmd.Value}");
                }
                break;
            case "inject-error":
                var errorType = cmd.Error?.ToLower();
                _logger.ZLogInformation($"Injecting error: {errorType}");
                switch (errorType)
                {
                    case "jam":
                        hardwareStatusManager.SetJammed(true);
                        break;
                    case "overlap":
                        hardwareStatusManager.SetOverlapped(true);
                        break;
                    case "none":
                    default:
                        hardwareStatusManager.SetJammed(false);
                        hardwareStatusManager.SetOverlapped(false);
                        break;
                }
                break;
            case "assert":
                var target = cmd.Target?.ToLower();
                var expected = ResolveValue(cmd.Value, context);
                _logger.ZLogInformation($"Asserting {target} == {expected}");
                if (target == "inventory")
                {
                    var denomValue = ResolveValue(cmd.Denom ?? 0, context);
                    var key = new DenominationKey(denomValue, cmd.Type?.ToLower() == "coin" ? CashType.Coin : CashType.Bill, cmd.Currency ?? "JPY");
                    var count = inventory.GetCount(key);
                    if (count != expected)
                    {
                        throw new Exception($"Assert failed: Inventory count for {key} is {count}, expected {expected}");
                    }
                }
                else if (target == "status")
                {
                    // Add more status checks if needed
                }
                break;
            case "begindeposit":
                depositController.BeginDeposit();
                break;
            case "trackdeposit":
                var type = cmd.Type?.ToLower() == "coin" ? CashType.Coin : CashType.Bill;
                var value = ResolveValue(cmd.Value, context);
                var keyDeposit = new DenominationKey(value, type, cmd.Currency ?? "JPY");
                _logger.ZLogDebug($"TrackDeposit: {keyDeposit} (Count: {cmd.Count})");
                depositController.TrackBulkDeposit(new Dictionary<DenominationKey, int> { { keyDeposit, cmd.Count } });
                await Task.Delay(250); // Simulate hardware processing time to allow UI updates
                break;
            case "fixdeposit":
                depositController.FixDeposit();
                break;
            case "enddeposit":
                var action = cmd.Action?.ToLower() switch
                {
                    "repay" => CashDepositAction.Repay,
                    _ => CashDepositAction.NoChange // Default to Store
                };
                _logger.ZLogDebug($"EndDeposit Action: {action}");
                depositController.EndDeposit(action);
                break;
            case "dispense":
                var dispenseValue = ResolveValue(cmd.Value, context);
                _logger.ZLogDebug($"Dispense: {dispenseValue}");
                await dispenseController.DispenseChangeAsync(dispenseValue, false, (c, ex) => { });
                break;
            case "delay":
                _logger.ZLogDebug($"Delaying for {cmd.Value}ms");
                await Task.Delay(ResolveValue(cmd.Value, context));
                break;
        }
    }

    private int ResolveValue(object value, ScriptExecutionContext context)
    {
        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number)
            {
                return element.GetInt32();
            }
            if (element.ValueKind == JsonValueKind.String)
            {
                var str = element.GetString();
                if (str != null && str.StartsWith("$"))
                {
                    var varName = str.Substring(1);
                    if (context.Variables.TryGetValue(varName, out var varValue))
                    {
                        if (varValue is JsonElement varElement && varElement.ValueKind == JsonValueKind.Number)
                        {
                            return varElement.GetInt32();
                        }
                        return Convert.ToInt32(varValue);
                    }
                }
                if (int.TryParse(str, out var parsedInt))
                {
                    return parsedInt;
                }
            }
        }
        return Convert.ToInt32(value);
    }

    private class ScriptCommand
    {
        public string Op { get; set; } = "";
        public string? Currency { get; set; }
        public object Value { get; set; } = 0;
        public int Count { get; set; }
        public string? Type { get; set; } // "Bill" or "Coin"
        public string? Action { get; set; } // "Store" or "Repay"
        public string? Variable { get; set; }
        public List<ScriptCommand>? Commands { get; set; }
        public string? Error { get; set; } // For Inject-Error
        public string? Target { get; set; } // For Assert
        public object? Denom { get; set; } // For Assert
    }
}
