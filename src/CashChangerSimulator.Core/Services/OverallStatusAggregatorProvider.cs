using CashChangerSimulator.Core.Models;
using CashChangerSimulator.Core.Monitoring;

namespace CashChangerSimulator.Core.Services;

/// <summary>全体的なステータス集計インスタンスを提供するプロバイダー。</summary>
/// <param name="monitorsProvider">ステータスモニタープロバイダー。</param>
public class OverallStatusAggregatorProvider(MonitorsProvider monitorsProvider)
{
    /// <summary>ステータス集計インスタンス。</summary>
    public OverallStatusAggregator Aggregator { get; } = new OverallStatusAggregator(monitorsProvider.Monitors);
}
