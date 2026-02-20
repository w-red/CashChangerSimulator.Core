using CashChangerSimulator.Core.Models;

namespace CashChangerSimulator.Core;

/// <summary>プロジェクトを跨いでシングルトンインスタンスを共有するための簡易サービスロケーター。</summary>
public static class ServiceLocator
{
    /// <summary>在庫管理のシングルトンインスタンス。</summary>
    public static Inventory? Inventory { get; set; }
    /// <summary>取引履歴管理のシングルトンインスタンス。</summary>
    public static TransactionHistory? History { get; set; }
    /// <summary>出金マネージャーのシングルトンインスタンス。</summary>
    public static CashChangerManager? Manager { get; set; }
    /// <summary>ハードウェア状態管理のシングルトンインスタンス。</summary>
    public static HardwareStatusManager? HardwareStatusManager { get; set; }
}
