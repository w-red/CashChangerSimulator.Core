using CashChangerSimulator.Core.Managers;
using CashChangerSimulator.Core.Models;

namespace CashChangerSimulator.Core.Services;

/// <summary>釣銭機デバイスを生成するための抽象ファクトリインターフェース。</summary>
public interface ICashChangerDeviceFactory
{
    /// <summary>指定された依存コンポーネントを使用して、新しい釣銭機デバイスのインスタンスを生成します。</summary>
    /// <param name="manager">釣銭機マネージャー。</param>
    /// <param name="inventory">現金在庫。</param>
    /// <param name="statusManager">ハードウェア状態管理。</param>
    /// <returns>生成された釣銭機デバイスのインスタンス。</returns>
    ICashChangerDevice Create(CashChangerManager manager, Inventory inventory, HardwareStatusManager statusManager);
}
