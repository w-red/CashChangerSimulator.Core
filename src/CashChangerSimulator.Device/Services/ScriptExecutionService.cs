using CashChangerSimulator.Core;
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
public class ScriptExecutionService(DepositController depositController, DispenseController dispenseController) : IScriptExecutionService
{
    private readonly ILogger<ScriptExecutionService> _logger = LogProvider.CreateLogger<ScriptExecutionService>();
    public async Task ExecuteScriptAsync(string json)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var commands = JsonSerializer.Deserialize<List<ScriptCommand>>(json, options);
        if (commands == null)
        {
            _logger.ZLogError($"Failed to deserialize script JSON.");
            return;
        }

        _logger.ZLogInformation($"Starting script execution. Commands count: {commands.Count}");

        foreach (var cmd in commands)
        {
            await ExecuteCommandAsync(cmd);
        }
        
        _logger.ZLogInformation($"Finished script execution.");
    }

    private async Task ExecuteCommandAsync(ScriptCommand cmd)
    {
        _logger.ZLogDebug($"Executing command: {cmd.Op}");
        switch (cmd.Op.ToLower())
        {
            case "begindeposit":
                depositController.BeginDeposit();
                break;
            case "trackdeposit":
                var type = cmd.Type?.ToLower() == "coin" ? CashType.Coin : CashType.Bill;
                var key = new DenominationKey(cmd.Value, type, cmd.Currency ?? "JPY");
                _logger.ZLogDebug($"TrackDeposit: {key} (Count: {cmd.Count})");
                depositController.TrackBulkDeposit(new Dictionary<DenominationKey, int> { { key, cmd.Count } });
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
                _logger.ZLogDebug($"Dispense: {cmd.Value}");
                await dispenseController.DispenseChangeAsync(cmd.Value, false, (c, ex) => { });
                break;
            case "delay":
                _logger.ZLogDebug($"Delaying for {cmd.Value}ms");
                await Task.Delay(cmd.Value);
                break;
        }
    }

    private class ScriptCommand
    {
        public string Op { get; set; } = "";
        public string? Currency { get; set; }
        public int Value { get; set; }
        public int Count { get; set; }
        public string? Type { get; set; } // "Bill" or "Coin"
        public string? Action { get; set; } // "Store" or "Repay"
    }
}
