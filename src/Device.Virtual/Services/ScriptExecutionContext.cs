namespace CashChangerSimulator.Device.Virtual.Services;

/// <summary>スクリプト実行中の変数コンテキスト。.</summary>
public class ScriptExecutionContext
{
    /// <summary>Gets スクリプト変数の辞書を取得します。.</summary>
    public Dictionary<string, object> Variables { get; } = [];
}
