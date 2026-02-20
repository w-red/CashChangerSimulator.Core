using R3;

namespace CashChangerSimulator.Core.Models;

/// <summary>特定の金種の枚数を監視し、しきい値に基づいた状態（CashStatus）を通知する。</summary>
public class CashStatusMonitor : IDisposable
{
    private readonly IReadOnlyInventory _inventory;
    private readonly DenominationKey _key;
    private int _nearEmptyThreshold;
    private int _nearFullThreshold;
    private int _fullThreshold;
    private readonly ReactiveProperty<CashStatus> _status = new(CashStatus.Unknown);
    private readonly IDisposable _subscription;

    /// <summary>現在の状態を流すイベントストリーム。</summary>
    public ReadOnlyReactiveProperty<CashStatus> Status => _status;

    /// <summary>監視する金種キー。</summary>
    public DenominationKey Key => _key;

    /// <summary>在庫、金種キー、各種しきい値を指定してインスタンスを初期化する。</summary>
    public CashStatusMonitor(IReadOnlyInventory inventory, DenominationKey key, int nearEmptyThreshold, int nearFullThreshold, int fullThreshold)
    {
        _inventory = inventory;
        _key = key;
        _nearEmptyThreshold = nearEmptyThreshold;
        _nearFullThreshold = nearFullThreshold;
        _fullThreshold = fullThreshold;

        // 初回計算
        UpdateStatus();

        // 在庫変更時の再計算
        _subscription = _inventory.Changed
            .Where(k => k == _key)
            .Subscribe(_ => UpdateStatus());
    }

    private void UpdateStatus()
    {
        var count = _inventory.GetCount(_key);

        if (count == 0)
        {
            _status.Value = CashStatus.Empty;
        }
        else if (count < _nearEmptyThreshold)
        {
            _status.Value = CashStatus.NearEmpty;
        }
        else if (count >= _fullThreshold)
        {
            _status.Value = CashStatus.Full;
        }
        else if (count >= _nearFullThreshold)
        {
            _status.Value = CashStatus.NearFull;
        }
        else
        {
            _status.Value = CashStatus.Normal;
        }
    }

    /// <summary>しきい値を動的に更新し、状態を再計算します。</summary>
    public void UpdateThresholds(int nearEmpty, int nearFull, int full)
    {
        _nearEmptyThreshold = nearEmpty;
        _nearFullThreshold = nearFull;
        _fullThreshold = full;
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
