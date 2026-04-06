namespace CashChangerSimulator.Core.Configuration;

/// <summary>履歴の永続化に関する設定。</summary>
public class HistorySettings
{
    /// <summary>保持する履歴の最大件数。</summary>
    public int MaxEntries { get; set; } = 1000;
}
