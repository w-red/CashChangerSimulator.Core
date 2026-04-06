using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace CashChangerSimulator.Device.Virtual.Services.ScriptCommands;

/// <summary>inject-error コマンド: ハードウェアエラーを注入します。</summary>
/// <param name="hardwareStatusManager">ハードウェア状態管理インスタンス。</param>
public class InjectErrorCommandHandler(HardwareStatusManager hardwareStatusManager) : IScriptCommandHandler
{
    /// <summary>Gets コマンド名を取得します。</summary>
    public string OpName => "INJECTERROR";

    /// <summary>スクリプトコマンドを実行します。</summary>
    /// <param name="cmd">コマンド。</param>
    /// <param name="context">実行コンテキスト。</param>
    /// <param name="logger">ロガー。</param>
    /// <param name="onProgress">進行状況を通知するコールバック。</param>
    /// <returns>非同期タスク。</returns>
    public Task ExecuteAsync(ScriptCommand cmd, ScriptExecutionContext context, ILogger logger, Action<string>? onProgress)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        var errorType = cmd.Error?.ToUpperInvariant();
        logger.ZLogInformation($"Injecting error: {errorType}");
        switch (errorType)
        {
            case "JAM":
                var location = JamLocation.None;
                if (!string.IsNullOrEmpty(cmd.Location))
                {
                    Enum.TryParse(cmd.Location, true, out location);
                }

                hardwareStatusManager.SetJammed(true, location);
                break;
            case "OVERLAP":
                hardwareStatusManager.SetOverlapped(true);
                break;
            case "DEVICE":
                var code = cmd.ErrorCode != null ? ScriptExecutionService.ResolveValue(cmd.ErrorCode, context) : 0;
                var extended = cmd.ErrorCodeExtended != null ? ScriptExecutionService.ResolveValue(cmd.ErrorCodeExtended, context) : 0;
                hardwareStatusManager.SetDeviceError(code, extended);
                break;
            case "NONE":
            case "RESET":
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
