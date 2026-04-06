using R3;

namespace CashChangerSimulator.Core.Monitoring;

/// <summary>釣銭機全体のステータスを集約管理するクラス。.</summary>
public class OverallStatusAggregator : IDisposable
{
    private readonly BindableReactiveProperty<CashStatus> deviceStatus = new(CashStatus.Unknown);
    private readonly BindableReactiveProperty<CashStatus> fullStatus = new(CashStatus.Unknown);
    private IEnumerable<CashStatusMonitor> monitors;
    private IDisposable? currentSubscription;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="OverallStatusAggregator"/> class.監視対象のモニター一覧を指定して初期化します。.</summary>
    /// <param name="monitors">監視対象のモニター一覧。.</param>
    public OverallStatusAggregator(IEnumerable<CashStatusMonitor> monitors)
    {
        this.monitors = monitors;
        DeviceStatus = deviceStatus.ToReadOnlyReactiveProperty();
        FullStatus = fullStatus.ToReadOnlyReactiveProperty();

        Refresh(monitors);
    }

    /// <summary>Gets 空・ニアエンプティに関する集約ステータス。.</summary>
    public ReadOnlyReactiveProperty<CashStatus> DeviceStatus { get; }

    /// <summary>Gets 満杯・ニアフルに関する集約ステータス。.</summary>
    public ReadOnlyReactiveProperty<CashStatus> FullStatus { get; }

    /// <summary>金種モニターのリストを更新し、集計ロジックを再構築します。.</summary>
    /// <param name="monitors">新しく監視対象とするモニター一覧。.</param>
    public void Refresh(IEnumerable<CashStatusMonitor> monitors)
    {
        currentSubscription?.Dispose();
        this.monitors = monitors;

        if (!this.monitors.Any())
        {
            deviceStatus.Value = CashStatus.Unknown;
            fullStatus.Value = CashStatus.Unknown;
            return;
        }

        // 最初の状態を即座に計算する (Initial calculation)
        deviceStatus.Value = AggregateDevice(this.monitors);
        fullStatus.Value = AggregateFull(this.monitors);

        currentSubscription = Observable.CombineLatest(this.monitors.Select(m => m.Status.AsObservable()))
            .Subscribe(_ =>
            {
                deviceStatus.Value = AggregateDevice(this.monitors);
                fullStatus.Value = AggregateFull(this.monitors);
            });
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>リソースを破棄します。.</summary>
    /// <param name="disposing">マネージリソースを破棄する場合は true。.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        if (disposing)
        {
            currentSubscription?.Dispose();
            deviceStatus.Dispose();
            fullStatus.Dispose();
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
