using CashChangerSimulator.Core.Managers;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace CashChangerSimulator.Device.Services.ScriptCommands;

/// <summary>inject-error コマンド: ハードウェアエラーを注入します。</summary>
public class InjectErrorCommandHandler(HardwareStatusManager hardwareStatusManager) : IScriptCommandHandler
{
    /// <summary>コマンド名を取得します。</summary>
    public string OpName => "inject-error";

    /// <summary>スクリプトコマンドを実行します。</summary>
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
