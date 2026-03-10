namespace CashChangerSimulator.Device.Services;

/// <summary>スクリプト実行中の変数コンテキスト。</summary>
public class ScriptExecutionContext
{
    /// <summary>スクリプト変数の辞書を取得します。</summary>
    public Dictionary<string, object> Variables { get; } = [];
}
