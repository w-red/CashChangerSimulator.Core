using System.Text.Json;
using CashChangerSimulator.Core;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Device.Virtual.Services.ScriptCommands;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace CashChangerSimulator.Device.Virtual.Services;

/// <summary>スクリプトデータに基づいてシミュレーターの操作を自動実行するサービス。</summary>
public class ScriptExecutionService(
    DepositController depositController,
    DispenseController dispenseController,
    Inventory inventory,
    HardwareStatusManager hardwareStatusManager) : IScriptExecutionService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    private readonly ILogger<ScriptExecutionService> logger =
        LogProvider.CreateLogger<ScriptExecutionService>();

    private readonly Dictionary<string, IScriptCommandHandler> handlers =
        InitializeHandlers(
            depositController,
            dispenseController,
            inventory,
            hardwareStatusManager);

    /// <summary>変数参照を解決して整数値を返します。</summary>
    /// <param name="value">値オブジェクト。</param>
    /// <param name="context">コンテキスト。</param>
    /// <returns>解決された整数値。</returns>
    public static int ResolveValue(
        object value,
        ScriptExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(context);

        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number)
            {
                return element.GetInt32();
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                var str = element.GetString();
                if (str != null && str.StartsWith('$'))
                {
                    var varName = str[1..];
                    if (context.Variables.TryGetValue(varName, out var varValue))
                    {
                        return
                            varValue is JsonElement varElement
                            && varElement.ValueKind == JsonValueKind.Number
                                ? varElement.GetInt32()
                                : Convert.ToInt32(varValue, System.Globalization.CultureInfo.InvariantCulture);
                    }
                }

                if (int.TryParse(str, out var parsedInt))
                {
                    return parsedInt;
                }
            }
        }

        return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>JSON スクリプトを解析して実行します。</summary>
    /// <param name="json">JSON 文字列。</param>
    /// <param name="onProgress">進行状況を通知するコールバック。</param>
    /// <returns>非同期操作。</returns>
    public async Task ExecuteScriptAsync(
        string json,
        Action<string>? onProgress = null)
    {
        var commands = JsonSerializer.Deserialize<List<ScriptCommand>>(json, JsonOptions);
        if (commands == null)
        {
            logger.ZLogError($"Failed to deserialize script JSON.");
            return;
        }

        logger.ZLogInformation($"Starting script execution. Commands count: {commands.Count}");

        var context = new ScriptExecutionContext();
        await ExecuteCommandsInternalAsync(commands, context, onProgress).ConfigureAwait(false);

        logger.ZLogInformation($"Finished script execution.");
    }

    /// <summary>
    /// 内部でコマンドリストを実行します。
    /// </summary>
    /// <param name="commands">コマンドリスト。</param>
    /// <param name="context">実行コンテキスト。</param>
    /// <param name="onProgress">プログレス通知。</param>
    /// <returns>非同期タスク。</returns>
    internal async Task ExecuteCommandsInternalAsync(
        IEnumerable<ScriptCommand> commands,
        ScriptExecutionContext context,
        Action<string>? onProgress)
    {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(context);

        foreach (var cmd in commands)
        {
            await ExecuteCommandAsync(cmd, context, onProgress).ConfigureAwait(false);
        }
    }

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

    /// <summary>単一のコマンドを実行します。</summary>
    /// <param name="cmd">コマンド。</param>
    /// <param name="context">コンテキスト。</param>
    /// <param name="onProgress">通知。</param>
    /// <returns>タスク。</returns>
    private async Task ExecuteCommandAsync(
        ScriptCommand cmd,
        ScriptExecutionContext context,
        Action<string>? onProgress)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        ArgumentNullException.ThrowIfNull(context);

        logger.ZLogDebug($"Executing command: {cmd.Op}");
        onProgress?.Invoke(cmd.Op);

        var opName = cmd.Op.ToUpperInvariant().Replace("-", string.Empty, StringComparison.Ordinal);

        // Special case: repeat needs access to ExecuteCommandsInternalAsync
        if (opName == "REPEAT" && cmd.Commands != null)
        {
            var iterations = cmd.Count != null ? ResolveValue(cmd.Count, context) : 0;
            logger.ZLogInformation($"Starting repeat loop: {iterations} times.");
            for (int i = 0; i < iterations; i++)
            {
                await ExecuteCommandsInternalAsync(cmd.Commands, context, onProgress).ConfigureAwait(false);
            }

            logger.ZLogInformation($"Finished repeat loop.");
            return;
        }

        // identifiers などの正規化は ToUpperInvariant が推奨される (CA1308)
        if (handlers.TryGetValue(opName, out var handler))
        {
            await handler.ExecuteAsync(cmd, context, logger, onProgress).ConfigureAwait(false);
        }
    }
}
