using CashChangerSimulator.Core.Managers;
using Microsoft.Extensions.Logging;

namespace CashChangerSimulator.Device.Virtual.Services.ScriptCommands;

/// <summary>enable コマンド: デバイスを有効状態にします。.</summary>
public class EnableCommandHandler(HardwareStatusManager hardwareStatusManager) : IScriptCommandHandler
{
    /// <summary>Gets コマンド名を取得します。.</summary>
    public string OpName => "ENABLE";

    /// <summary>スクリプトコマンドを実行します。.</summary>
    /// <param name="cmd">コマンド。.</param>
    /// <param name="context">実行コンテキスト。.</param>
    /// <param name="logger">ロガー。.</param>
    /// <param name="onProgress">進行状況を通知するコールバック。.</param>
    /// <returns>非同期タスク。.</returns>
    public Task ExecuteAsync(ScriptCommand cmd, ScriptExecutionContext context, ILogger logger, Action<string>? onProgress)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        hardwareStatusManager.SetConnected(true); // Open status
        return Task.CompletedTask;
    }
}
