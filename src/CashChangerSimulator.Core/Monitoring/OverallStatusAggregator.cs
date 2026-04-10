using R3;

namespace CashChangerSimulator.Core.Monitoring;

/// <summary>釣銭機全体のステータスを集約管理するクラス。</summary>
public class OverallStatusAggregator : IDisposable
{
    private readonly CompositeDisposable disposables = [];
    private IEnumerable<CashStatusMonitor> monitors;
    private bool disposed;

    private OverallStatusAggregator(IEnumerable<CashStatusMonitor> monitors)
    {
        this.monitors = monitors;

        var deviceStatusProp = new BindableReactiveProperty<CashStatus>(CashStatus.Unknown);
        disposables.Add(deviceStatusProp);
        DeviceStatusProperty = deviceStatusProp;

        var fullStatusProp = new BindableReactiveProperty<CashStatus>(CashStatus.Unknown);
        disposables.Add(fullStatusProp);
        FullStatusProperty = fullStatusProp;

        var subscriptionDisposable = new SerialDisposable();
        disposables.Add(subscriptionDisposable);
        CurrentSubscriptionProperty = subscriptionDisposable;

        DeviceStatus = deviceStatusProp.ToReadOnlyReactiveProperty().AddTo(disposables);
        FullStatus = fullStatusProp.ToReadOnlyReactiveProperty().AddTo(disposables);
    }

    /// <summary>空・ニアエンプティに関する集約ステータス。</summary>
    public ReadOnlyReactiveProperty<CashStatus> DeviceStatus { get; }

    /// <summary>満杯・ニアフルに関する集約ステータス。</summary>
    public ReadOnlyReactiveProperty<CashStatus> FullStatus { get; }

    private Observable<CashStatus> DeviceStatusProperty { get; }
    private Observable<CashStatus> FullStatusProperty { get; }
    private IDisposable CurrentSubscriptionProperty { get; }

    /// <summary>監視対象のモニター一覧を指定してインスタンスを生成・初期化します。</summary>
    /// <param name="monitors">監視対象のモニター一覧。</param>
    /// <returns>初期化済みの <see cref="OverallStatusAggregator"/> インスタンス。</returns>
    public static OverallStatusAggregator Create(IEnumerable<CashStatusMonitor> monitors)
    {
        var instance = new OverallStatusAggregator(monitors);
        instance.Refresh(monitors);
        return instance;
    }

    /// <summary>金種モニターのリストを更新し、集計ロジックを再構築します。</summary>
    /// <param name="monitors">新しく監視対象とするモニター一覧。</param>
    public void Refresh(IEnumerable<CashStatusMonitor> monitors)
    {
        ((SerialDisposable)CurrentSubscriptionProperty).Disposable = Disposable.Empty;
        this.monitors = monitors;

        if (!this.monitors.Any())
        {
            ((BindableReactiveProperty<CashStatus>)DeviceStatusProperty).Value = CashStatus.Unknown;
            ((BindableReactiveProperty<CashStatus>)FullStatusProperty).Value = CashStatus.Unknown;
            return;
        }

        // 最初の状態を即座に計算する (Initial calculation)
        ((BindableReactiveProperty<CashStatus>)DeviceStatusProperty).Value = AggregateDevice(this.monitors);
        ((BindableReactiveProperty<CashStatus>)FullStatusProperty).Value = AggregateFull(this.monitors);

        ((SerialDisposable)CurrentSubscriptionProperty).Disposable = Observable.CombineLatest(this.monitors.Select(m => m.Status.AsObservable()))
            .Subscribe(_ =>
            {
                ((BindableReactiveProperty<CashStatus>)DeviceStatusProperty).Value = AggregateDevice(this.monitors);
                ((BindableReactiveProperty<CashStatus>)FullStatusProperty).Value = AggregateFull(this.monitors);
            });
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>リソースを破棄します。</summary>
    /// <param name="disposing">マネージリソースを破棄する場合は true。</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        if (disposing)
        {
            disposables.Dispose();
        }

        disposed = true;
    }

    private static CashStatus AggregateDevice(IEnumerable<CashStatusMonitor> monitors)
    {
        var recyclableMonitors = monitors.Where(m => m.IsRecyclable).ToList();
        if (recyclableMonitors.Count == 0)
        {
            return CashStatus.Normal;
        }

        var statuses = recyclableMonitors.Select(m => m.Status.CurrentValue).ToList();

        // 1. If any is Empty -> Empty
        if (statuses.Exists(s => s == CashStatus.Empty))
        {
            return CashStatus.Empty;
        }

        // 2. If any is NearEmpty -> NearEmpty
        if (statuses.Exists(s => s == CashStatus.NearEmpty))
        {
            return CashStatus.NearEmpty;
        }

        // 3. Otherwise Normal
        return CashStatus.Normal;
    }

    private static CashStatus AggregateFull(IEnumerable<CashStatusMonitor> monitors)
    {
        // ニアフル・満杯についても、リサイクル可能（払出に関わる）な金種のみを対象とする
        // ※回収庫の満杯監視が必要な場合は別途ロジックが必要だが、現状の要求は在庫（リサイクル）に関するもの
        var recyclableStatuses = monitors.Where(m => m.IsRecyclable).Select(m => m.Status.CurrentValue).ToList();

        if (recyclableStatuses.Count == 0)
        {
            return CashStatus.Normal;
        }

        return recyclableStatuses.Exists(s => s == CashStatus.Full)
            ? CashStatus.Full
            : recyclableStatuses.Exists(s => s == CashStatus.NearFull) ? CashStatus.NearFull : CashStatus.Normal;
    }
}
