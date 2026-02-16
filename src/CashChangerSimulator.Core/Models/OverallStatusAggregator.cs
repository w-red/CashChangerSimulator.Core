using R3;

namespace CashChangerSimulator.Core.Models;

/// <summary>
/// 釣銭機全体のステータスを集約管理するクラス。
/// </summary>
public class OverallStatusAggregator : IDisposable
{
    private readonly IEnumerable<CashStatusMonitor> _monitors;
    private readonly ReadOnlyReactiveProperty<CashStatus> _overallStatus;

    /// <summary>
    /// 全体の集約ステータス。
    /// </summary>
    public ReadOnlyReactiveProperty<CashStatus> OverallStatus => _overallStatus;

    /// <summary>
    /// コンストラクタ。
    /// </summary>
    /// <param name="monitors">監視対象の各金種モニター。</param>
    public OverallStatusAggregator(IEnumerable<CashStatusMonitor> monitors)
    {
        _monitors = monitors;

        // 全てのモニターの状態を監視し、最も深刻な状態を全体状態とする
        _overallStatus = Observable.CombineLatest(_monitors.Select(m => m.Status.AsObservable()))
            .Select(statuses => Aggregate(statuses))
            .ToReadOnlyReactiveProperty(CashStatus.Unknown);
    }

    private static CashStatus Aggregate(IList<CashStatus> statuses)
    {
        if (statuses.Any(s => s == CashStatus.Full)) return CashStatus.Full;
        if (statuses.Any(s => s == CashStatus.Empty)) return CashStatus.Empty;
        if (statuses.Any(s => s == CashStatus.NearFull)) return CashStatus.NearFull;
        if (statuses.Any(s => s == CashStatus.NearEmpty)) return CashStatus.NearEmpty;
        
        return CashStatus.Normal;
    }

    public void Dispose()
    {
        _overallStatus.Dispose();
    }
}
