using CashChangerSimulator.Core.Models;
using R3;

namespace CashChangerSimulator.Core.Monitoring;

/// <summary>特定の金種の枚数を監視し、しきい値に基づいた状態（CashStatus）を通知する。</summary>
public class CashStatusMonitor : IDisposable
{
    private readonly IReadOnlyInventory _inventory;

    /// <summary>この金種がリサイクル可能かどうか。</summary>
    public bool IsRecyclable { get; }
    /// <summary>ニアエンプティしきい値。</summary>
    public int NearEmptyThreshold { get; private set; }
    /// <summary>ニアフルしきい値。</summary>
    public int NearFullThreshold { get; private set; }
    /// <summary>フルしきい値。</summary>
    public int FullThreshold { get; private set; }

    private readonly ReactiveProperty<CashStatus> _status = new(CashStatus.Unknown);
    private readonly IDisposable _subscription;

    /// <summary>現在の状態を流すイベントストリーム。</summary>
    public ReadOnlyReactiveProperty<CashStatus> Status => _status;

    /// <summary>監視する金種キー。</summary>
    public DenominationKey Key { get; }

    /// <summary>在庫、金種キー、各種しきい値を指定してインスタンスを初期化する。</summary>
    public CashStatusMonitor(IReadOnlyInventory inventory, DenominationKey key, int nearEmptyThreshold, int nearFullThreshold, int fullThreshold, bool isRecyclable = true)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(key);

        _inventory = inventory;
        Key = key;
        IsRecyclable = isRecyclable;
        NearEmptyThreshold = nearEmptyThreshold;
        NearFullThreshold = nearFullThreshold;
        FullThreshold = fullThreshold;

        // 初回計算
        UpdateStatus();

        // 在庫変更時の再計算
        _subscription = _inventory.Changed
            .Where(k => k == Key)
            .Subscribe(_ => UpdateStatus());
    }

    private void UpdateStatus()
    {
        var count = _inventory.GetCount(Key);

        if (NearEmptyThreshold != -1 && count == 0)
        {
            _status.Value = CashStatus.Empty;
        }
        else if (NearEmptyThreshold != -1 && count < NearEmptyThreshold)
        {
            _status.Value = CashStatus.NearEmpty;
        }
        else if (FullThreshold != -1 && count >= FullThreshold)
        {
            _status.Value = CashStatus.Full;
        }
        else
        {
            _status.Value = NearFullThreshold != -1 && count >= NearFullThreshold ? CashStatus.NearFull : CashStatus.Normal;
        }
    }

    /// <summary>しきい値を動的に更新し、状態を再計算します。</summary>
    public void UpdateThresholds(int nearEmpty, int nearFull, int full)
    {
        NearEmptyThreshold = nearEmpty;
        NearFullThreshold = nearFull;
        FullThreshold = full;
        UpdateStatus();
    }

    /// <summary>購読やリソースを解放する。</summary>
    public void Dispose()
    {
        _subscription.Dispose();
        _status.Dispose();
        GC.SuppressFinalize(this);
    }
}
