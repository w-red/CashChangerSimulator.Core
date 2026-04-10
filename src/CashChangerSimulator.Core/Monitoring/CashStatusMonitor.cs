using CashChangerSimulator.Core.Models;
using R3;

namespace CashChangerSimulator.Core.Monitoring;

/// <summary>特定の金種の枚数を監視し、しきい値に基づいた状態（CashStatus）を通知する。</summary>
public class CashStatusMonitor : IDisposable
{
    private readonly IReadOnlyInventory inventory;
    private readonly ReactiveProperty<CashStatus> status = new(CashStatus.Unknown);
    private readonly CompositeDisposable disposables = [];
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="CashStatusMonitor"/> class.在庫、金種キー、各種しきい値を指定してインスタンスを初期化する。</summary>
    /// <param name="inventory">監視対象の在庫。</param>
    /// <param name="key">監視対象の金種キー。</param>
    /// <param name="nearEmptyThreshold">ニアエンプティしきい値。</param>
    /// <param name="nearFullThreshold">ニアフルしきい値。</param>
    /// <param name="fullThreshold">フルしきい値。</param>
    /// <param name="isRecyclable">この金種がリサイクル可能かどうか。</param>
    public CashStatusMonitor(IReadOnlyInventory inventory, DenominationKey key, int nearEmptyThreshold, int nearFullThreshold, int fullThreshold, bool isRecyclable = true)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(key);

        this.inventory = inventory;
        Key = key;
        IsRecyclable = isRecyclable;
        NearEmptyThreshold = nearEmptyThreshold;
        NearFullThreshold = nearFullThreshold;
        FullThreshold = fullThreshold;

        status.AddTo(disposables);
        UpdateStatus();

        // 在庫変更時の再計算
        inventory.Changed
            .Where(k => k == Key)
            .Subscribe(_ => UpdateStatus())
            .AddTo(disposables);
    }

    /// <summary>この金種がリサイクル可能かどうか。</summary>
    public bool IsRecyclable { get; }

    /// <summary>ニアエンプティしきい値。</summary>
    public int NearEmptyThreshold { get; private set; }

    /// <summary>ニアフルしきい値。</summary>
    public int NearFullThreshold { get; private set; }

    /// <summary>フルしきい値。</summary>
    public int FullThreshold { get; private set; }

    /// <summary>現在の状態を流すイベントストリーム。</summary>
    public ReadOnlyReactiveProperty<CashStatus> Status =>
        status.ToReadOnlyReactiveProperty().AddTo(disposables);

    /// <summary>監視する金種キー。</summary>
    public DenominationKey Key { get; }

    /// <summary>しきい値を動的に更新し、状態を再計算します。</summary>
    /// <param name="nearEmpty">新しいニアエンプティしきい値。</param>
    /// <param name="nearFull">新しいニアフルしきい値。</param>
    /// <param name="full">新しいフルしきい値。</param>
    public void UpdateThresholds(int nearEmpty, int nearFull, int full)
    {
        NearEmptyThreshold = nearEmpty;
        NearFullThreshold = nearFull;
        FullThreshold = full;
        UpdateStatus();
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

    private void UpdateStatus()
    {
        var count = inventory.GetCount(Key);

        if (NearEmptyThreshold != -1 && count == 0)
        {
            status.Value = CashStatus.Empty;
        }
        else if (NearEmptyThreshold != -1 && count < NearEmptyThreshold)
        {
            status.Value = CashStatus.NearEmpty;
        }
        else if (FullThreshold != -1 && count >= FullThreshold)
        {
            status.Value = CashStatus.Full;
        }
        else
        {
            status.Value = NearFullThreshold != -1 && count >= NearFullThreshold ? CashStatus.NearFull : CashStatus.Normal;
        }
    }
}
