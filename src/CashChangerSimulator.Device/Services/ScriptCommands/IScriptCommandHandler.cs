using CashChangerSimulator.Device.Services;
using Microsoft.Extensions.Logging;

namespace CashChangerSimulator.Device.Services.ScriptCommands;

/// <summary>スクリプトコマンドを処理するハンドラーのインターフェース。</summary>
public interface IScriptCommandHandler
{
    /// <summary>対応するコマンド名（小文字）。</summary>
    string OpName { get; }

    /// <summary>コマンドを実行します。</summary>
    Task ExecuteAsync(ScriptCommand cmd, ScriptExecutionContext context, ILogger logger, Action<string>? onProgress);
}
