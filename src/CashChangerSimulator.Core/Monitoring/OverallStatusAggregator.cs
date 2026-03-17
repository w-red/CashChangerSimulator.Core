using R3;

namespace CashChangerSimulator.Core.Monitoring;

/// <summary>釣銭機全体のステータスを集約管理するクラス。</summary>
public class OverallStatusAggregator : IDisposable
{
    private IEnumerable<CashStatusMonitor> _monitors;
    private readonly BindableReactiveProperty<CashStatus> _deviceStatus;
    private readonly BindableReactiveProperty<CashStatus> _fullStatus;
    private IDisposable? _currentSubscription;

    /// <summary>空・ニアエンプティに関する集約ステータス。</summary>
    public ReadOnlyReactiveProperty<CashStatus> DeviceStatus { get; }

    /// <summary>満杯・ニアフルに関する集約ステータス。</summary>
    public ReadOnlyReactiveProperty<CashStatus> FullStatus { get; }

    /// <summary>監視対象のモニター一覧を指定して初期化します。</summary>
    public OverallStatusAggregator(IEnumerable<CashStatusMonitor> monitors)
    {
        _monitors = monitors;
        _deviceStatus = new BindableReactiveProperty<CashStatus>(CashStatus.Unknown);
        _fullStatus = new BindableReactiveProperty<CashStatus>(CashStatus.Unknown);
        DeviceStatus = _deviceStatus.ToReadOnlyReactiveProperty();
        FullStatus = _fullStatus.ToReadOnlyReactiveProperty();

        Refresh(monitors);
    }

    /// <summary>金種モニターのリストを更新し、集計ロジックを再構築します。</summary>
    public void Refresh(IEnumerable<CashStatusMonitor> monitors)
    {
        _currentSubscription?.Dispose();
        _monitors = monitors;

        if (!_monitors.Any())
        {
            _deviceStatus.Value = CashStatus.Unknown;
            _fullStatus.Value = CashStatus.Unknown;
            return;
        }

        _currentSubscription = Observable.CombineLatest(_monitors.Select(m => m.Status.AsObservable()))
            .Subscribe(s =>
            {
                _deviceStatus.Value = AggregateDevice(s);
                _fullStatus.Value = AggregateFull(s);
            });
    }

    private static CashStatus AggregateDevice(IList<CashStatus> statuses)
    {
        return statuses.Any(s => s == CashStatus.Empty)
            ? CashStatus.Empty
            : statuses.Any(s => s == CashStatus.NearEmpty) ? CashStatus.NearEmpty : CashStatus.Normal;
    }

    private static CashStatus AggregateFull(IList<CashStatus> statuses)
    {
        return statuses.Any(s => s == CashStatus.Full)
            ? CashStatus.Full
            : statuses.Any(s => s == CashStatus.NearFull) ? CashStatus.NearFull : CashStatus.Normal;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _currentSubscription?.Dispose();
        _deviceStatus.Dispose();
        _fullStatus.Dispose();
        GC.SuppressFinalize(this);
    }
}
