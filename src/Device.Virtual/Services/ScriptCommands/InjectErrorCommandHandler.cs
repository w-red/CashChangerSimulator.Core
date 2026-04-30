using CashChangerSimulator.Core.Exceptions;
using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;
using Microsoft.Extensions.Logging;
using R3;
using ZLogger;

namespace CashChangerSimulator.Device.Virtual.Services.ScriptCommands;

/// <summary>inject-error コマンド: ハードウェアエラーを注入します。</summary>
/// <param name="hardwareStatusManager">ハードウェア状態管理インスタンス。</param>
public class InjectErrorCommandHandler(
    HardwareStatusManager hardwareStatusManager)
    : IScriptCommandHandler
{
    /// <summary>コマンド名を取得します。</summary>
    public ScriptCommandType OpName =>
        ScriptCommandType.InjectError;

    /// <summary>スクリプトコマンドを実行します。</summary>
    /// <param name="cmd">コマンド。</param>
    /// <param name="context">実行コンテキスト。</param>
    /// <param name="logger">ロガー。</param>
    /// <param name="onProgress">進行状況を通知するコールバック。</param>
    /// <returns>非同期タスク。</returns>
    public Task ExecuteAsync(
        ScriptCommand cmd,
        ScriptExecutionContext context,
        ILogger logger,
        Action<string>? onProgress)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        var errorType = ScriptErrorType.FromString(cmd.Error);
        logger?.ZLogInformation($"Injecting error: {errorType}");

        if (errorType == ScriptErrorType.Jam)
        {
            var location = JamLocation.None;
            if (!string.IsNullOrEmpty(cmd.Location))
            {
                Enum.TryParse(cmd.Location, true, out location);
            }

            if (!hardwareStatusManager.IsConnected.CurrentValue)
            {
                throw new DeviceException("Device is not connected.", DeviceErrorCode.Failure);
            }

            hardwareStatusManager.Input.IsJammed.Value = true;
            hardwareStatusManager.Input.CurrentJamLocation.Value = location;
        }
        else if (errorType == ScriptErrorType.Overlap)
        {
            hardwareStatusManager.Input.IsOverlapped.Value = true;
        }
        else if (errorType == ScriptErrorType.Device)
        {
            var code = cmd.ErrorCode != null ? ScriptExecutionService.ResolveValue(cmd.ErrorCode, context) : 0;
            var extended = cmd.ErrorCodeExtended != null ? ScriptExecutionService.ResolveValue(cmd.ErrorCodeExtended, context) : 0;
            hardwareStatusManager.Input.CurrentErrorCode.Value = code;
            hardwareStatusManager.Input.CurrentErrorCodeExtended.Value = extended;
            hardwareStatusManager.Input.IsDeviceError.Value = true;
        }
        else if (errorType == ScriptErrorType.None || errorType == ScriptErrorType.Reset)
        {
            hardwareStatusManager.Input.ResetTrigger.OnNext(Unit.Default);
        }
        else
        {
            hardwareStatusManager.Input.IsJammed.Value = false;
            hardwareStatusManager.Input.IsOverlapped.Value = false;
        }

        return Task.CompletedTask;
    }
}
