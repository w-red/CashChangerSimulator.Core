using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace CashChangerSimulator.Device.Services.ScriptCommands;

/// <summary>inject-error コマンド: ハードウェアエラーを注入します。</summary>
/// <param name="hardwareStatusManager">ハードウェア状態管理インスタンス。</param>
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
                var location = JamLocation.None;
                if (!string.IsNullOrEmpty(cmd.Location))
                {
                    Enum.TryParse<JamLocation>(cmd.Location, true, out location);
                }
                hardwareStatusManager.SetJammed(true, location);
                break;
            case "overlap":
                hardwareStatusManager.SetOverlapped(true);
                break;
            case "device":
                hardwareStatusManager.SetDeviceError(cmd.ErrorCode ?? 0, cmd.ErrorCodeExtended ?? 0);
                break;
            case "none":
            case "reset":
                hardwareStatusManager.ResetError();
                break;
            default:
                hardwareStatusManager.SetJammed(false);
                hardwareStatusManager.SetOverlapped(false);
                break;
        }
        return Task.CompletedTask;
    }
}
