using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;

namespace CashChangerSimulator.Device.Virtual;

/// <summary>出金(払出)時の計算と在庫反映を担当するクラス。</summary>
/// <param name="manager">キャッシュチェンジャーマネージャー。</param>
/// <param name="hardwareStatusManager">ハードウェア状態管理。</param>
internal sealed class DispenseCalculator(
    CashChangerManager manager,
    HardwareStatusManager hardwareStatusManager)
{
    /// <summary>払い出し処理を反映します。</summary>
    /// <param name="dispenseCounts">払い出す金種と枚数。</param>
    /// <param name="isRepay">返却処理かどうか。</param>
    public void ProcessDispense(
        IReadOnlyDictionary<DenominationKey, int> dispenseCounts,
        bool isRepay)
    {
        // 在庫の減算
        manager.Dispense(dispenseCounts);

        // 出金口の状態を更新
        // isRepay が true の場合は回収口、それ以外は通常口
        var targetPort =
            isRepay ? ExitPort.Collection : ExitPort.Normal;
        hardwareStatusManager.Input.AddExitPortCounts(targetPort, dispenseCounts);
    }
}
