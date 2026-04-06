using CashChangerSimulator.Core.Monitoring;
using R3;

namespace CashChangerSimulator.Core.Services;

/// <summary>全体的なステータス集計インスタンスを提供するプロバイダー。</summary>
public class OverallStatusAggregatorProvider : IDisposable
{
    private readonly CompositeDisposable disposables = [];

    /// <summary>Initializes a new instance of the <see cref="OverallStatusAggregatorProvider"/> class.全体的なステータス集計インスタンスを提供するプロバイダー。</summary>
    /// <param name="monitorsProvider">ステータスモニタープロバイダー。</param>
    public OverallStatusAggregatorProvider(MonitorsProvider monitorsProvider)
    {
        ArgumentNullException.ThrowIfNull(monitorsProvider);
        Aggregator = new OverallStatusAggregator(monitorsProvider.Monitors);
        monitorsProvider.Changed.Subscribe(_ => Aggregator.Refresh(monitorsProvider.Monitors)).AddTo(disposables);
    }

    /// <summary>Gets ステータス集計インスタンス。</summary>
    public OverallStatusAggregator Aggregator { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>リソースを解放します。</summary>
    /// <param name="disposing">明示的な破棄かどうか。</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            disposables.Dispose();
            Aggregator.Dispose();
        }
    }
}
