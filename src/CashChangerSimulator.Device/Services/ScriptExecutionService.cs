using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device.Services.ScriptCommands;
using Microsoft.Extensions.Logging;
using Microsoft.PointOfService;
using System.Text.Json;
using ZLogger;

namespace CashChangerSimulator.Device.Services;

/// <summary>スクリプトコマンドのデータモデル。</summary>
public class ScriptCommand
{
    public string Op { get; set; } = "";
    public string? Currency { get; set; }
    public object Value { get; set; } = 0;
    public int Count { get; set; }
    public string? Type { get; set; }
    public string? Action { get; set; }
    public string? Variable { get; set; }
    public List<ScriptCommand>? Commands { get; set; }
    public string? Error { get; set; }
    public string? Target { get; set; }
    public object? Denom { get; set; }
}

/// <summary>スクリプト実行中の変数コンテキスト。</summary>
public class ScriptExecutionContext
{
    /// <summary>スクリプト変数の辞書。</summary>
    public Dictionary<string, object> Variables { get; } = new();
}

/// <summary>スクリプトデータに基づいてシミュレーターの操作を自動実行するサービス。</summary>
public class ScriptExecutionService(
    DepositController depositController,
    DispenseController dispenseController,
    Inventory inventory,
    HardwareStatusManager hardwareStatusManager) : IScriptExecutionService
{
    private readonly ILogger<ScriptExecutionService> _logger = LogProvider.CreateLogger<ScriptExecutionService>();
    private readonly Dictionary<string, IScriptCommandHandler> _handlers = InitializeHandlers(
        depositController, dispenseController, inventory, hardwareStatusManager);

    private static Dictionary<string, IScriptCommandHandler> InitializeHandlers(
        DepositController depositController,
        DispenseController dispenseController,
        Inventory inventory,
        HardwareStatusManager hardwareStatusManager)
    {
        var handlers = new IScriptCommandHandler[]
        {
            new OpenCommandHandler(hardwareStatusManager),
            new SetCommandHandler(),
            new InjectErrorCommandHandler(hardwareStatusManager),
            new AssertCommandHandler(inventory),
            new BeginDepositCommandHandler(depositController),
            new TrackDepositCommandHandler(depositController),
            new FixDepositCommandHandler(depositController),
            new EndDepositCommandHandler(depositController),
            new DispenseCommandHandler(dispenseController),
            new DelayCommandHandler(),
        };
        return handlers.ToDictionary(h => h.OpName, h => h);
    }

    /// <summary>JSON スクリプトを解析して実行します。</summary>
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

    internal async Task ExecuteCommandsInternalAsync(IEnumerable<ScriptCommand> commands, ScriptExecutionContext context, Action<string>? onProgress)
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

        var opName = cmd.Op.ToLower();

        // Special case: repeat needs access to ExecuteCommandsInternalAsync
        if (opName == "repeat" && cmd.Commands != null)
        {
            _logger.ZLogInformation($"Starting repeat loop: {cmd.Count} times.");
            for (int i = 0; i < cmd.Count; i++)
            {
                await ExecuteCommandsInternalAsync(cmd.Commands, context, onProgress);
            }
            _logger.ZLogInformation($"Finished repeat loop.");
            return;
        }

        if (_handlers.TryGetValue(opName, out var handler))
        {
            await handler.ExecuteAsync(cmd, context, _logger, onProgress);
        }
    }

    /// <summary>変数参照を解決して整数値を返します。</summary>
    public static int ResolveValue(object value, ScriptExecutionContext context)
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
}
