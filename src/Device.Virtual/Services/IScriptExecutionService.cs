namespace CashChangerSimulator.Device.Virtual.Services;

/// <summary>シミュレーターでの自動スクリプト実行のためのコントラクトを定義します。</summary>
public interface IScriptExecutionService
{
    /// <summary>スクリプトを非同期で実行します。</summary>
    /// <param name="json">実行対象の JSON 文字列。</param>
    /// <param name="onProgress">進行状況を通知するコールバック(任意)。</param>
    /// <returns>完了を示すタスク。</returns>
    Task ExecuteScriptAsync(string json, Action<string>? onProgress = null);
}
