using Microsoft.Extensions.Logging;

namespace CashChangerSimulator.Device.Virtual.Services.ScriptCommands;

/// <summary>スクリプトコマンドを処理するハンドラーのインターフェース。</summary>
public interface IScriptCommandHandler
{
    /// <summary>対応するコマンド名（大文字）。</summary>
    string OpName { get; }

    /// <summary>コマンドを実行します。</summary>
    /// <param name="cmd">コマンド。</param>
    /// <param name="context">実行コンテキスト。</param>
    /// <param name="logger">ロガー。</param>
    /// <param name="onProgress">進行状況を通知するコールバック。</param>
    /// <returns>非同期タスク。</returns>
    Task ExecuteAsync(ScriptCommand cmd, ScriptExecutionContext context, ILogger logger, Action<string>? onProgress);
}
