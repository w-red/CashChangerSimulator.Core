using R3;

namespace CashChangerSimulator.Core.Models;

/// <summary>
/// 特定の金種の枚数を監視し、しきい値に基づいた状態（CashStatus）を通知するクラス。
/// </summary>
public class CashStatusMonitor : IDisposable
{
    private readonly IReadOnlyInventory _inventory;
    private readonly int _denomination;
    private readonly int _nearEmptyThreshold;
    private readonly int _nearFullThreshold;
    private readonly int _fullThreshold;
    private readonly ReactiveProperty<CashStatus> _status = new(CashStatus.Unknown);
    private readonly IDisposable _subscription;

    /// <summary>
    /// 現在の状態を流すイベントストリーム。
    /// </summary>
    public ReadOnlyReactiveProperty<CashStatus> Status => _status;

    /// <summary>
    /// コンストラクタ。
    /// </summary>
    /// <param name="inventory">監視対象の在庫。</param>
    /// <param name="denomination">監視する金種。</param>
    /// <param name="nearEmptyThreshold">NearEmpty と判定するしきい値（この枚数未満なら NearEmpty）。</param>
    /// <param name="nearFullThreshold">NearFull と判定するしきい値（この枚数以上なら NearFull）。</param>
    /// <param name="fullThreshold">Full と判定するしきい値（この枚数以上なら Full）。</param>
    public CashStatusMonitor(IReadOnlyInventory inventory, int denomination, int nearEmptyThreshold, int nearFullThreshold, int fullThreshold)
    {
        _inventory = inventory;
        _denomination = denomination;
        _nearEmptyThreshold = nearEmptyThreshold;
        _nearFullThreshold = nearFullThreshold;
        _fullThreshold = fullThreshold;

        // 初回計算
        UpdateStatus();

        // 在庫変更時の再計算
        _subscription = _inventory.Changed
            .Where(d => d == _denomination)
            .Subscribe(_ => UpdateStatus());
    }

    private void UpdateStatus()
    {
        var count = _inventory.GetCount(_denomination);
        
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

    public void Dispose()
    {
        _subscription.Dispose();
        _status.Dispose();
    }
}
