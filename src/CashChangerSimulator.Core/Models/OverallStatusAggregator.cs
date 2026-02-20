using R3;

namespace CashChangerSimulator.Core.Models;

/// <summary>釣銭機全体のステータスを集約管理するクラス。</summary>
public class OverallStatusAggregator : IDisposable
{
    private readonly IEnumerable<CashStatusMonitor> _monitors;
    private readonly ReadOnlyReactiveProperty<CashStatus> _deviceStatus;
    private readonly ReadOnlyReactiveProperty<CashStatus> _fullStatus;

    /// <summary>空・ニアエンプティに関する集約ステータス。</summary>
    public ReadOnlyReactiveProperty<CashStatus> DeviceStatus => _deviceStatus;

    /// <summary>満杯・ニアフルに関する集約ステータス。</summary>
    public ReadOnlyReactiveProperty<CashStatus> FullStatus => _fullStatus;

    public OverallStatusAggregator(IEnumerable<CashStatusMonitor> monitors)
    {
        _monitors = monitors;
        var statuses = Observable.CombineLatest(_monitors.Select(m => m.Status.AsObservable()));

        _deviceStatus = statuses
            .Select(s => AggregateDevice(s))
            .ToReadOnlyReactiveProperty(CashStatus.Unknown);

        _fullStatus = statuses
            .Select(s => AggregateFull(s))
            .ToReadOnlyReactiveProperty(CashStatus.Unknown);
    }

    private static CashStatus AggregateDevice(IList<CashStatus> statuses)
    {
        if (statuses.Any(s => s == CashStatus.Empty)) return CashStatus.Empty;
        if (statuses.Any(s => s == CashStatus.NearEmpty)) return CashStatus.NearEmpty;
        return CashStatus.Normal;
    }

    private static CashStatus AggregateFull(IList<CashStatus> statuses)
    {
        if (statuses.Any(s => s == CashStatus.Full)) return CashStatus.Full;
        if (statuses.Any(s => s == CashStatus.NearFull)) return CashStatus.NearFull;
        return CashStatus.Normal;
    }

    public void Dispose()
    {
        _deviceStatus.Dispose();
        _fullStatus.Dispose();
        GC.SuppressFinalize(this);
    }
}
