using CashChangerSimulator.Core.Managers;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace CashChangerSimulator.Device.Services.ScriptCommands;

/// <summary>open コマンド: デバイスを接続状態にします。</summary>
public class OpenCommandHandler(HardwareStatusManager hardwareStatusManager) : IScriptCommandHandler
{
    /// <summary>コマンド名を取得します。</summary>
    public string OpName => "open";

    /// <summary>スクリプトコマンドを実行します。</summary>
    public Task ExecuteAsync(ScriptCommand cmd, ScriptExecutionContext context, ILogger logger, Action<string>? onProgress)
    {
        logger.ZLogInformation($"Opening device via script.");
        hardwareStatusManager.SetConnected(true);
        return Task.CompletedTask;
    }
}
