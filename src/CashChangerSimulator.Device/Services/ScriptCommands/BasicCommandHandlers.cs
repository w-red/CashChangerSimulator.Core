using CashChangerSimulator.Core.Managers;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace CashChangerSimulator.Device.Services.ScriptCommands;

/// <summary>open コマンド: デバイスを接続状態にします。</summary>
public class OpenCommandHandler(HardwareStatusManager hardwareStatusManager) : IScriptCommandHandler
{
    public string OpName => "open";

    public Task ExecuteAsync(ScriptCommand cmd, ScriptExecutionContext context, ILogger logger, Action<string>? onProgress)
    {
        logger.ZLogInformation($"Opening device via script.");
        hardwareStatusManager.SetConnected(true);
        return Task.CompletedTask;
    }
}

/// <summary>set コマンド: 変数に値を格納します。</summary>
public class SetCommandHandler : IScriptCommandHandler
{
    public string OpName => "set";

    public Task ExecuteAsync(ScriptCommand cmd, ScriptExecutionContext context, ILogger logger, Action<string>? onProgress)
    {
        if (!string.IsNullOrEmpty(cmd.Variable))
        {
            context.Variables[cmd.Variable] = cmd.Value;
            logger.ZLogInformation($"Set variable: {cmd.Variable} = {cmd.Value}");
        }
        return Task.CompletedTask;
    }
}

/// <summary>inject-error コマンド: ハードウェアエラーを注入します。</summary>
public class InjectErrorCommandHandler(HardwareStatusManager hardwareStatusManager) : IScriptCommandHandler
{
    public string OpName => "inject-error";

    public Task ExecuteAsync(ScriptCommand cmd, ScriptExecutionContext context, ILogger logger, Action<string>? onProgress)
    {
        var errorType = cmd.Error?.ToLower();
        logger.ZLogInformation($"Injecting error: {errorType}");
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
        return Task.CompletedTask;
    }
}

/// <summary>delay コマンド: 指定ミリ秒待機します。</summary>
public class DelayCommandHandler : IScriptCommandHandler
{
    public string OpName => "delay";

    public async Task ExecuteAsync(ScriptCommand cmd, ScriptExecutionContext context, ILogger logger, Action<string>? onProgress)
    {
        var ms = ScriptExecutionService.ResolveValue(cmd.Value, context);
        logger.ZLogDebug($"Delaying for {ms}ms");
        await Task.Delay(ms);
    }
}
