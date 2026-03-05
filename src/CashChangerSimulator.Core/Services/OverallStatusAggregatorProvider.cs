using CashChangerSimulator.Core.Monitoring;
using R3;

namespace CashChangerSimulator.Core.Services;

/// <summary>全体的なステータス集計インスタンスを提供するプロバイダー。</summary>
/// <param name="monitorsProvider">ステータスモニタープロバイダー。</param>
public class OverallStatusAggregatorProvider
{
    /// <summary>ステータス集計インスタンス。</summary>
    public OverallStatusAggregator Aggregator { get; }

    /// <summary>全体的なステータス集計インスタンスを提供するプロバイダー。</summary>
    public OverallStatusAggregatorProvider(MonitorsProvider monitorsProvider)
    {
        Aggregator = new OverallStatusAggregator(monitorsProvider.Monitors);
        monitorsProvider.Changed.Subscribe(_ => Aggregator.Refresh(monitorsProvider.Monitors));
    }
}
